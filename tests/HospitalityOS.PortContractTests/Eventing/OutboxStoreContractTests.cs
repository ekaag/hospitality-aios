using HospitalityOS.PortContractTests.Persistence;
using HospitalityOS.PortContractTests.Persistence.Support;
using HospitalityOS.Ports.Eventing;
using HospitalityOS.SharedKernel.Domain;
using Xunit;

namespace HospitalityOS.PortContractTests.Eventing;

/// <summary>
/// The dispatcher's view of the outbox: due events are claimed oldest first under a lease, retried later, or
/// dead-lettered; every operation applies at once and never saves the scope's other pending changes.
/// </summary>
public abstract class OutboxStoreContractTests : PersistenceContractTests
{
    private static readonly TimeSpan lease = TimeSpan.FromMinutes(1);

    [Fact]
    public async Task ClaimDue_ReturnsDueEventsOldestFirstUpToMax()
    {
        await QueueAsync(TestEvents.Message(1), TestEvents.Message(2), TestEvents.Message(3));

        IReadOnlyList<OutboxEntry> claimed = await ClaimAsync(TestData.Now, maxCount: 2);

        Assert.Equal([TestEvents.Message(1), TestEvents.Message(2)], claimed.Select(entry => entry.Message));
    }

    [Fact]
    public async Task ClaimDue_EventsOfEveryTenant_AreClaimed()
    {
        await QueueAsync(TestEvents.Message(1, TestData.TenantA), TestEvents.Message(2, TestData.TenantB));

        IReadOnlyList<OutboxEntry> claimed = await ClaimAsync(TestData.Now);

        Assert.Equal(2, claimed.Count);
    }

    [Fact]
    public async Task ClaimDue_ClaimedEvent_IsNotClaimedAgainUntilLeaseExpires()
    {
        await QueueAsync(TestEvents.Message(1));
        await ClaimAsync(TestData.Now);

        IReadOnlyList<OutboxEntry> duringLease = await ClaimAsync(TestData.Now.AddSeconds(30));
        IReadOnlyList<OutboxEntry> afterLease = await ClaimAsync(TestData.Now + lease);

        Assert.Empty(duringLease);
        Assert.Single(afterLease);
    }

    [Fact]
    public async Task MarkDispatched_RemovesEventFromQueue()
    {
        await QueueAsync(TestEvents.Message(1));
        await ClaimAsync(TestData.Now);

        await WithOutboxAsync(outbox => outbox.MarkDispatchedAsync(TestEvents.Message(1).EventId, Cancellation));
        await WithOutboxAsync(outbox => outbox.MarkDispatchedAsync(TestEvents.Message(1).EventId, Cancellation));

        Assert.Empty(await ClaimAsync(TestData.Now.AddHours(1)));
    }

    [Fact]
    public async Task ScheduleRetry_ReleasesLeaseAndDelaysUntilRetryTime()
    {
        await QueueAsync(TestEvents.Message(1));
        await ClaimAsync(TestData.Now);

        await WithOutboxAsync(outbox => outbox.ScheduleRetryAsync(TestEvents.Message(1).EventId, TestData.Now.AddMinutes(5), "broker unavailable", Cancellation));

        Assert.Empty(await ClaimAsync(TestData.Now.AddMinutes(4)));
        Assert.Equal([new OutboxEntry(TestEvents.Message(1), 1, "broker unavailable")], await ClaimAsync(TestData.Now.AddMinutes(5)));
    }

    [Fact]
    public async Task DeadLetter_EventIsNeverClaimedAgain()
    {
        await QueueAsync(TestEvents.Message(1));
        await ClaimAsync(TestData.Now);

        await WithOutboxAsync(outbox => outbox.DeadLetterAsync(TestEvents.Message(1).EventId, TestData.Now, "payload rejected", Cancellation));

        Assert.Empty(await ClaimAsync(TestData.Now.AddDays(1)));
    }

    [Fact]
    public async Task ListDeadLetters_ReturnsTheTenantsGivenUpEvents_MostRecentFirst_WithTheirLastError()
    {
        await QueueAsync(TestEvents.Message(1), TestEvents.Message(2), TestEvents.Message(3, TestData.TenantB), TestEvents.Message(4));
        await ClaimAsync(TestData.Now);
        await WithOutboxAsync(outbox => outbox.ScheduleRetryAsync(TestEvents.Message(1).EventId, TestData.Now, "broker unavailable", Cancellation));
        await WithOutboxAsync(outbox => outbox.DeadLetterAsync(TestEvents.Message(1).EventId, TestData.Now.AddMinutes(2), "payload rejected", Cancellation));
        await WithOutboxAsync(outbox => outbox.DeadLetterAsync(TestEvents.Message(2).EventId, TestData.Now.AddMinutes(5), "too large", Cancellation));
        await WithOutboxAsync(outbox => outbox.DeadLetterAsync(TestEvents.Message(3, TestData.TenantB).EventId, TestData.Now, "other tenant", Cancellation));

        IReadOnlyList<DeadLetteredOutboxEvent> deadLetters = await ListAsync(TestData.TenantA);

        Assert.Equal(
            [
                new DeadLetteredOutboxEvent(TestEvents.Message(2), 0, "too large", TestData.Now.AddMinutes(5)),
                new DeadLetteredOutboxEvent(TestEvents.Message(1), 1, "payload rejected", TestData.Now.AddMinutes(2)),
            ],
            deadLetters);
        Assert.Single(await ListAsync(TestData.TenantA, maxCount: 1));
    }

    [Fact]
    public async Task Requeue_MakesADeadLetteredEventDueAtOnce_AsIfNew_ButOnlyForItsTenant()
    {
        await QueueAsync(TestEvents.Message(1), TestEvents.Message(2));
        await ClaimAsync(TestData.Now);
        await WithOutboxAsync(outbox => outbox.ScheduleRetryAsync(TestEvents.Message(1).EventId, TestData.Now, "broker unavailable", Cancellation));
        await WithOutboxAsync(outbox => outbox.DeadLetterAsync(TestEvents.Message(1).EventId, TestData.Now, "payload rejected", Cancellation));
        bool otherTenant = false, notDeadLettered = true, requeued = false, again = true;

        await WithOutboxAsync(async outbox =>
        {
            otherTenant = await outbox.RequeueAsync(TestData.TenantB, TestEvents.Message(1).EventId, Cancellation);
            notDeadLettered = await outbox.RequeueAsync(TestData.TenantA, TestEvents.Message(2).EventId, Cancellation);
            requeued = await outbox.RequeueAsync(TestData.TenantA, TestEvents.Message(1).EventId, Cancellation);
            again = await outbox.RequeueAsync(TestData.TenantA, TestEvents.Message(1).EventId, Cancellation);
        });

        Assert.Equal((false, false, true, false), (otherTenant, notDeadLettered, requeued, again));
        Assert.Empty(await ListAsync(TestData.TenantA));
        OutboxEntry entry = Assert.Single(await ClaimAsync(TestData.Now.AddSeconds(1)), claimed => claimed.Message == TestEvents.Message(1));
        Assert.Equal((0, "payload rejected"), (entry.Attempts, entry.LastError));
    }

    [Fact]
    public async Task Operations_DoNotSaveTheScopesPendingChanges()
    {
        await QueueAsync(TestEvents.Message(1));
        AggregateCase organizations = AggregateCases.Get("Organization");
        IAggregateRoot pending = organizations.Create(TestData.TenantA, 1);

        await using (PersistenceSession session = OpenSession())
        {
            await organizations.AddAsync(session, pending, Cancellation);
            session.Get<IEventStore>().Append(TestEvents.Message(2));
            IOutboxStore outbox = session.Get<IOutboxStore>();
            await outbox.ClaimDueAsync(TestData.Now, 10, lease, Cancellation);
            await outbox.ScheduleRetryAsync(TestEvents.Message(1).EventId, TestData.Now, "retry", Cancellation);
            await outbox.MarkDispatchedAsync(TestEvents.Message(1).EventId, Cancellation);
        }

        await using PersistenceSession reader = OpenSession();

        Assert.Null(await organizations.GetAsync(reader, organizations.IdOf(pending), Cancellation));
        Assert.Empty(await reader.Get<IEventStore>().ReadAsync(new EventStoreQuery(AfterPosition: 1), Cancellation));
    }

    [Fact]
    public async Task ClaimDue_InvalidArguments_Throw()
    {
        await using PersistenceSession session = OpenSession();
        IOutboxStore outbox = session.Get<IOutboxStore>();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => outbox.ClaimDueAsync(TestData.Now, 0, lease, Cancellation));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => outbox.ClaimDueAsync(TestData.Now, 1, TimeSpan.Zero, Cancellation));
    }

    private async Task QueueAsync(params EventMessage[] messages)
    {
        await using PersistenceSession session = OpenSession();

        foreach (EventMessage message in messages)
        {
            session.Get<IEventStore>().Append(message);
        }

        Assert.True((await session.CommitAsync(Cancellation)).IsSuccess);
    }

    private async Task<IReadOnlyList<OutboxEntry>> ClaimAsync(DateTimeOffset now, int maxCount = 10)
    {
        await using PersistenceSession session = OpenSession();

        return await session.Get<IOutboxStore>().ClaimDueAsync(now, maxCount, lease, Cancellation);
    }

    private async Task<IReadOnlyList<DeadLetteredOutboxEvent>> ListAsync(SharedKernel.Tenancy.TenantId tenantId, int maxCount = 10)
    {
        await using PersistenceSession session = OpenSession();

        return await session.Get<IOutboxStore>().ListDeadLettersAsync(tenantId, maxCount, Cancellation);
    }

    private async Task WithOutboxAsync(Func<IOutboxStore, Task> action)
    {
        await using PersistenceSession session = OpenSession();
        await action(session.Get<IOutboxStore>());
    }
}
