using HospitalityOS.PortContractTests.Persistence;
using HospitalityOS.PortContractTests.Persistence.Support;
using HospitalityOS.Ports.Eventing;
using HospitalityOS.Ports.Persistence;
using HospitalityOS.SharedKernel.Domain;
using HospitalityOS.SharedKernel.Results;
using Xunit;

namespace HospitalityOS.PortContractTests.Eventing;

/// <summary>
/// Handler idempotency (P8, Journey 19): a processing record counts only once committed with the handler's
/// changes, is per handler, and makes a racing duplicate's whole commit fail.
/// </summary>
public abstract class InboxStoreContractTests : PersistenceContractTests
{
    private const string CompleteRequest = "GuestService.CompleteRequestOnHumanActionCompleted";
    private const string NotifyGuest = "Messaging.NotifyGuestOnServiceRequestCompleted";

    private static readonly Guid eventId = TestData.Id(1);

    [Fact]
    public async Task HasProcessed_IsTrueOnlyAfterTheRecordIsCommitted()
    {
        await using PersistenceSession session = OpenSession();
        IInboxStore inbox = session.Get<IInboxStore>();
        bool before = await inbox.HasProcessedAsync(CompleteRequest, eventId, Cancellation);
        inbox.Record(CompleteRequest, eventId, TestData.Now);
        bool staged = await inbox.HasProcessedAsync(CompleteRequest, eventId, Cancellation);

        await session.CommitAsync(Cancellation);

        Assert.False(before);
        Assert.False(staged);
        Assert.True(await HasProcessedAsync(CompleteRequest));
    }

    [Fact]
    public async Task HasProcessed_IsPerHandler()
    {
        await RecordAndCommitAsync(CompleteRequest);

        Assert.True(await HasProcessedAsync(CompleteRequest));
        Assert.False(await HasProcessedAsync(NotifyGuest));
    }

    [Fact]
    public async Task Record_WithoutCommit_IsNotSaved()
    {
        await using (PersistenceSession session = OpenSession())
        {
            session.Get<IInboxStore>().Record(CompleteRequest, eventId, TestData.Now);
        }

        Assert.False(await HasProcessedAsync(CompleteRequest));
    }

    [Fact]
    public async Task Commit_TwoDeliveriesRacingPastTheCheck_SecondFailsAndSavesNothing()
    {
        AggregateCase organizations = AggregateCases.Get("Organization");
        IAggregateRoot secondEffect = organizations.Create(TestData.TenantA, 2);
        await using PersistenceSession first = OpenSession();
        await using PersistenceSession second = OpenSession();
        Assert.False(await first.Get<IInboxStore>().HasProcessedAsync(CompleteRequest, eventId, Cancellation));
        Assert.False(await second.Get<IInboxStore>().HasProcessedAsync(CompleteRequest, eventId, Cancellation));
        await organizations.AddAsync(first, organizations.Create(TestData.TenantA, 1), Cancellation);
        first.Get<IInboxStore>().Record(CompleteRequest, eventId, TestData.Now);
        await organizations.AddAsync(second, secondEffect, Cancellation);
        second.Get<IInboxStore>().Record(CompleteRequest, eventId, TestData.Now);
        Assert.True((await first.CommitAsync(Cancellation)).IsSuccess);

        Result<CommitReceipt> commit = await second.CommitAsync(Cancellation);

        Assert.Equal(PersistenceErrors.DuplicateKeyCode, commit.Error.Code);
        await using PersistenceSession reader = OpenSession();
        Assert.Null(await organizations.GetAsync(reader, organizations.IdOf(secondEffect), Cancellation));
    }

    private async Task RecordAndCommitAsync(string handler)
    {
        await using PersistenceSession session = OpenSession();
        session.Get<IInboxStore>().Record(handler, eventId, TestData.Now);
        Assert.True((await session.CommitAsync(Cancellation)).IsSuccess);
    }

    private async Task<bool> HasProcessedAsync(string handler)
    {
        await using PersistenceSession session = OpenSession();

        return await session.Get<IInboxStore>().HasProcessedAsync(handler, eventId, Cancellation);
    }
}
