using HospitalityOS.PortContractTests.Persistence;
using HospitalityOS.PortContractTests.Persistence.Support;
using HospitalityOS.Ports.Eventing;
using HospitalityOS.Ports.Persistence;
using HospitalityOS.SharedKernel.Domain;
using HospitalityOS.SharedKernel.Results;
using Xunit;

namespace HospitalityOS.PortContractTests.Eventing;

/// <summary>
/// The event log: appends are saved only by the unit of work, together with the aggregate changes and in the outbox;
/// reads return committed events in log order, filtered and paged.
/// </summary>
public abstract class EventStoreContractTests : PersistenceContractTests
{
    [Fact]
    public async Task Read_AfterCommit_ReturnsAppendedEventsInOrder()
    {
        EventMessage[] appended = [TestEvents.Message(1), TestEvents.Message(2), TestEvents.Message(3)];
        await AppendAndCommitAsync(appended);

        IReadOnlyList<StoredEvent> read = await ReadAsync(new EventStoreQuery());

        Assert.Equal(appended, read.Select(stored => stored.Message));
        Assert.True(read.Zip(read.Skip(1)).All(pair => pair.First.Position < pair.Second.Position));
    }

    [Fact]
    public async Task Append_WithoutCommit_RecordsNothing()
    {
        await using (PersistenceSession session = OpenSession())
        {
            session.Get<IEventStore>().Append(TestEvents.Message(1));
        }

        Assert.Empty(await ReadAsync(new EventStoreQuery()));
    }

    [Fact]
    public async Task Commit_AppendedEvent_IsQueuedInOutbox()
    {
        EventMessage message = TestEvents.Message(1);
        await AppendAndCommitAsync(message);
        await using PersistenceSession session = OpenSession();

        IReadOnlyList<OutboxEntry> queued = await session.Get<IOutboxStore>().ClaimDueAsync(TestData.Now, 10, TimeSpan.FromMinutes(1), Cancellation);

        Assert.Equal([new OutboxEntry(message, 0, null)], queued);
    }

    [Fact]
    public async Task Commit_WithAggregate_SavesAggregateAndEventTogether()
    {
        AggregateCase organizations = AggregateCases.Get("Organization");
        IAggregateRoot organization = organizations.Create(TestData.TenantA, 1);
        await using (PersistenceSession session = OpenSession())
        {
            await organizations.AddAsync(session, organization, Cancellation);
            session.Get<IEventStore>().Append(TestEvents.Message(1));
            Assert.True((await session.CommitAsync(Cancellation)).IsSuccess);
        }

        await using PersistenceSession reader = OpenSession();

        Assert.NotNull(await organizations.GetAsync(reader, organizations.IdOf(organization), Cancellation));
        Assert.Equal(["HumanActionCompleted", "OrganizationCreated"], (await ReadAsync(new EventStoreQuery())).Select(stored => stored.Message.EventType));
    }

    [Fact]
    public async Task Commit_ChangedAggregate_RecordsAndQueuesEachDomainEvent()
    {
        AggregateCase organizations = AggregateCases.Get("Organization");
        IAggregateRoot organization = organizations.Create(TestData.TenantA, 1);
        organizations.Change(organization);
        Guid id = organizations.IdOf(organization);

        await SaveAsync(organization);

        IReadOnlyList<StoredEvent> recorded = await ReadAsync(new EventStoreQuery());
        Assert.Equal(["OrganizationCreated", "OrganizationUpdated"], recorded.Select(stored => stored.Message.EventType));
        Assert.All(recorded, stored => Assert.Equal(("Organization", id, TestData.TenantA), (stored.Message.AggregateType, stored.Message.AggregateId, stored.Message.TenantId)));
        await using PersistenceSession session = OpenSession();
        Assert.Equal(
            recorded.Select(stored => stored.Message),
            (await session.Get<IOutboxStore>().ClaimDueAsync(TestData.Now, 10, TimeSpan.FromMinutes(1), Cancellation)).Select(entry => entry.Message));
    }

    [Fact]
    public async Task Commit_FailedThenRetried_RecordsEachDomainEventOnce()
    {
        await SaveAsync(TestData.AccommodationUnit(TestData.TenantA, 1, "412"));
        await using PersistenceSession session = OpenSession();
        Domain.Properties.AccommodationUnits.AccommodationUnit clash = TestData.AccommodationUnit(TestData.TenantA, 2, "412");
        await session.Get<Domain.Properties.AccommodationUnits.IAccommodationUnitRepository>().AddAsync(clash, Cancellation);
        Assert.True((await session.CommitAsync(Cancellation)).IsFailure);
        clash.Renumber("413", TestData.Now);

        Result<CommitReceipt> retry = await session.CommitAsync(Cancellation);

        Assert.True(retry.IsSuccess);
        IReadOnlyList<StoredEvent> forClash = [.. (await ReadAsync(new EventStoreQuery())).Where(stored => stored.Message.AggregateId == clash.Id.Value)];
        Assert.Equal(["AccommodationUnitCreated", "AccommodationUnitUpdated"], forClash.Select(stored => stored.Message.EventType));
    }

    [Fact]
    public async Task Commit_WhenAggregateConflicts_RecordsAndQueuesNothing()
    {
        AggregateCase organizations = AggregateCases.Get("Organization");
        await SaveAsync(organizations.Create(TestData.TenantA, 1));
        await using PersistenceSession session = OpenSession();
        await organizations.AddAsync(session, organizations.Create(TestData.TenantA, 1), Cancellation);
        session.Get<IEventStore>().Append(TestEvents.Message(1));

        Result<CommitReceipt> commit = await session.CommitAsync(Cancellation);

        Assert.Equal(PersistenceErrors.DuplicateKeyCode, commit.Error.Code);
        Assert.Equal(["OrganizationCreated"], (await ReadAsync(new EventStoreQuery())).Select(stored => stored.Message.EventType));
        Assert.Equal(
            ["OrganizationCreated"],
            (await session.Get<IOutboxStore>().ClaimDueAsync(TestData.Now, 10, TimeSpan.FromMinutes(1), Cancellation)).Select(entry => entry.Message.EventType));
    }

    [Fact]
    public async Task Commit_EventIdAlreadyRecorded_ReturnsDuplicateKey()
    {
        await AppendAndCommitAsync(TestEvents.Message(1));
        await using PersistenceSession session = OpenSession();
        session.Get<IEventStore>().Append(TestEvents.Message(1));

        Result<CommitReceipt> commit = await session.CommitAsync(Cancellation);

        Assert.Equal(PersistenceErrors.DuplicateKeyCode, commit.Error.Code);
    }

    [Fact]
    public async Task Read_WithFilters_ReturnsOnlyMatchingEvents()
    {
        EventMessage flowA = TestEvents.Message(1, correlationId: TestEvents.FlowA);
        EventMessage flowB = TestEvents.Message(2, correlationId: TestEvents.FlowB);
        EventMessage otherTenant = TestEvents.Message(3, TestData.TenantB, TestEvents.FlowB);
        EventMessage later = TestEvents.Message(4, correlationId: TestEvents.FlowB, recordedAt: TestData.Now.AddHours(1));
        await AppendAndCommitAsync(flowA, flowB, otherTenant, later);

        IReadOnlyList<StoredEvent> byCorrelation = await ReadAsync(new EventStoreQuery(CorrelationId: TestEvents.FlowB, TenantId: TestData.TenantA));
        IReadOnlyList<StoredEvent> byTime = await ReadAsync(new EventStoreQuery(RecordedFrom: TestData.Now.AddSeconds(2), RecordedBefore: TestData.Now.AddMinutes(30)));

        Assert.Equal([flowB, later], byCorrelation.Select(stored => stored.Message));
        Assert.Equal([flowB, otherTenant], byTime.Select(stored => stored.Message));
    }

    [Fact]
    public async Task Read_AfterPosition_ContinuesWhereThePreviousPageEnded()
    {
        EventMessage[] appended = [.. Enumerable.Range(1, 5).Select(seed => TestEvents.Message(seed))];
        await AppendAndCommitAsync(appended);

        IReadOnlyList<StoredEvent> first = await ReadAsync(new EventStoreQuery(Limit: 2));
        IReadOnlyList<StoredEvent> second = await ReadAsync(new EventStoreQuery(AfterPosition: first[^1].Position, Limit: 2));

        Assert.Equal(appended[..2], first.Select(stored => stored.Message));
        Assert.Equal(appended[2..4], second.Select(stored => stored.Message));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(EventStoreQuery.MaxLimit + 1)]
    public async Task Read_LimitOutOfRange_Throws(int limit)
    {
        await using PersistenceSession session = OpenSession();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => session.Get<IEventStore>().ReadAsync(new EventStoreQuery(Limit: limit), Cancellation));
    }

    private async Task AppendAndCommitAsync(params EventMessage[] messages)
    {
        await using PersistenceSession session = OpenSession();

        foreach (EventMessage message in messages)
        {
            session.Get<IEventStore>().Append(message);
        }

        Assert.True((await session.CommitAsync(Cancellation)).IsSuccess);
    }

    private async Task<IReadOnlyList<StoredEvent>> ReadAsync(EventStoreQuery query)
    {
        await using PersistenceSession session = OpenSession();

        return await session.Get<IEventStore>().ReadAsync(query, Cancellation);
    }
}
