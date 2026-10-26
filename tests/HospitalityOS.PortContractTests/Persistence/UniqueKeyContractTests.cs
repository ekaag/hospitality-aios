using HospitalityOS.Domain.Capabilities.CapabilityInvocations;
using HospitalityOS.Domain.Guests.Conversations;
using HospitalityOS.Domain.Properties.AccommodationUnits;
using HospitalityOS.Domain.Services.ServiceRequests;
using HospitalityOS.PortContractTests.Persistence.Support;
using HospitalityOS.Ports.Persistence;
using HospitalityOS.SharedKernel.Domain;
using HospitalityOS.SharedKernel.Ids;
using HospitalityOS.SharedKernel.Results;
using Xunit;

namespace HospitalityOS.PortContractTests.Persistence;

/// <summary>
/// The unique keys the domain relies on hold even when two scopes race past the repositories' <c>Find…</c>
/// checks: the second commit is a <see cref="PersistenceErrors.DuplicateKeyCode"/> conflict (P8).
/// </summary>
public abstract class UniqueKeyContractTests : PersistenceContractTests
{
    [Fact]
    public async Task Commit_UnitNumberTakenInProperty_ReturnsDuplicateKey()
    {
        await SaveAsync(TestData.AccommodationUnit(TestData.TenantA, 1, "12a"));

        Result<CommitReceipt> commit = await AddAndCommitAsync(TestData.AccommodationUnit(TestData.TenantA, 2, "12A"));

        AssertDuplicateKey(commit);
    }

    [Fact]
    public async Task Commit_UnitNumberTakenInOtherProperty_Succeeds()
    {
        await SaveAsync(TestData.AccommodationUnit(TestData.TenantA, 1, "412"));

        Result<CommitReceipt> commit = await AddAndCommitAsync(TestData.AccommodationUnit(TestData.TenantA, 2, "412", TestData.OtherPropertyId));

        Assert.True(commit.IsSuccess);
    }

    [Fact]
    public async Task Commit_RenumberToTakenNumber_ReturnsDuplicateKey()
    {
        AccommodationUnit unit = TestData.AccommodationUnit(TestData.TenantA, 1, "412");
        await SaveAsync(unit, TestData.AccommodationUnit(TestData.TenantA, 2, "415"));
        await using PersistenceSession session = OpenSession();
        AccommodationUnit loaded = (await session.Get<IAccommodationUnitRepository>().GetByIdAsync(unit.Id, Cancellation))!;
        loaded.Renumber("415", TestData.Now);

        Result<CommitReceipt> commit = await session.CommitAsync(Cancellation);

        AssertDuplicateKey(commit);
    }

    [Fact]
    public async Task Commit_ServiceCodeTakenInProperty_ReturnsDuplicateKey()
    {
        await SaveAsync(TestData.Service(TestData.TenantA, 1, "housekeeping.amenities"));

        Result<CommitReceipt> commit = await AddAndCommitAsync(TestData.Service(TestData.TenantA, 2, "housekeeping.amenities"));

        AssertDuplicateKey(commit);
    }

    [Fact]
    public async Task Commit_ServiceCodeTakenInOtherProperty_Succeeds()
    {
        await SaveAsync(TestData.Service(TestData.TenantA, 1, "housekeeping.amenities"));

        Result<CommitReceipt> commit = await AddAndCommitAsync(TestData.Service(TestData.TenantA, 2, "housekeeping.amenities", TestData.OtherPropertyId));

        Assert.True(commit.IsSuccess);
    }

    [Fact]
    public async Task Commit_SecondHoursForSubject_ReturnsDuplicateKey()
    {
        await SaveAsync(TestData.OperatingHours(TestData.TenantA, 1, TestData.Pool));

        Result<CommitReceipt> commit = await AddAndCommitAsync(TestData.OperatingHours(TestData.TenantA, 2, TestData.Pool));

        AssertDuplicateKey(commit);
    }

    [Fact]
    public async Task Commit_ClientMessageIdTakenInConversation_ReturnsDuplicateKey()
    {
        Conversation conversation = TestData.Conversation(TestData.TenantA, 1);
        await SaveAsync(TestData.GuestMessage(TestData.TenantA, 1, conversation, "wa-1"));

        Result<CommitReceipt> commit = await AddAndCommitAsync(TestData.GuestMessage(TestData.TenantA, 2, conversation, "wa-1"));

        AssertDuplicateKey(commit);
    }

    [Fact]
    public async Task Commit_MessagesWithoutClientMessageId_Succeed()
    {
        Conversation conversation = TestData.Conversation(TestData.TenantA, 1);
        await SaveAsync(TestData.GuestMessage(TestData.TenantA, 1, conversation));

        Result<CommitReceipt> commit = await AddAndCommitAsync(TestData.GuestMessage(TestData.TenantA, 2, conversation));

        Assert.True(commit.IsSuccess);
    }

    [Fact]
    public async Task Commit_TwoScopesCreatingRequestFromSameMessage_SecondReturnsDuplicateKey()
    {
        GuestMessageId source = new(TestData.Id(1));
        await using PersistenceSession first = OpenSession();
        await using PersistenceSession second = OpenSession();
        Assert.Null(await first.Get<IServiceRequestRepository>().FindBySourceMessageAsync(source, Cancellation));
        Assert.Null(await second.Get<IServiceRequestRepository>().FindBySourceMessageAsync(source, Cancellation));
        await first.Get<IServiceRequestRepository>().AddAsync(TestData.ServiceRequest(TestData.TenantA, 1, source), Cancellation);
        await second.Get<IServiceRequestRepository>().AddAsync(TestData.ServiceRequest(TestData.TenantA, 2, source), Cancellation);
        Assert.True((await first.CommitAsync(Cancellation)).IsSuccess);

        Result<CommitReceipt> commit = await second.CommitAsync(Cancellation);

        AssertDuplicateKey(commit);
    }

    [Fact]
    public async Task Commit_RequestsWithoutSourceMessage_Succeed()
    {
        await SaveAsync(TestData.ServiceRequest(TestData.TenantA, 1));

        Result<CommitReceipt> commit = await AddAndCommitAsync(TestData.ServiceRequest(TestData.TenantA, 2));

        Assert.True(commit.IsSuccess);
    }

    [Fact]
    public async Task Commit_DuplicatesWithinOneCommit_ReturnsDuplicateKeyAndSavesNothing()
    {
        AccommodationUnit first = TestData.AccommodationUnit(TestData.TenantA, 1, "412");
        await using PersistenceSession session = OpenSession();
        IAccommodationUnitRepository units = session.Get<IAccommodationUnitRepository>();
        await units.AddAsync(first, Cancellation);
        await units.AddAsync(TestData.AccommodationUnit(TestData.TenantA, 2, "412"), Cancellation);

        Result<CommitReceipt> commit = await session.CommitAsync(Cancellation);

        AssertDuplicateKey(commit);
        await using PersistenceSession reader = OpenSession();
        Assert.Null(await reader.Get<IAccommodationUnitRepository>().GetByIdAsync(first.Id, Cancellation));
    }

    private static void AssertDuplicateKey(Result<CommitReceipt> commit)
    {
        Assert.True(commit.IsFailure);
        Assert.Equal(ErrorKind.Conflict, commit.Error.Kind);
        Assert.Equal(PersistenceErrors.DuplicateKeyCode, commit.Error.Code);
    }

    private async Task<Result<CommitReceipt>> AddAndCommitAsync(IAggregateRoot aggregate)
    {
        await using PersistenceSession session = OpenSession();
        await AggregateCases.Get(aggregate.GetType().Name).AddAsync(session, aggregate, Cancellation);

        return await session.CommitAsync(Cancellation);
    }

    [Fact]
    public async Task Commit_TwoScopesSucceedingWithSameIdempotencyKey_SecondReturnsDuplicateKey()
    {
        await using PersistenceSession first = OpenSession();
        await using PersistenceSession second = OpenSession();
        await first.Get<ICapabilityInvocationRepository>().AddAsync(TestData.CapabilityInvocation(TestData.TenantA, 1, "key-1", succeeded: true), Cancellation);
        await second.Get<ICapabilityInvocationRepository>().AddAsync(TestData.CapabilityInvocation(TestData.TenantA, 2, "key-1", succeeded: true), Cancellation);
        Assert.True((await first.CommitAsync(Cancellation)).IsSuccess);

        Result<CommitReceipt> commit = await second.CommitAsync(Cancellation);

        AssertDuplicateKey(commit);
    }

    [Fact]
    public async Task Commit_SameIdempotencyKeyFailedOrInAnotherCapabilityOrTenant_Succeeds()
    {
        await SaveAsync(TestData.CapabilityInvocation(TestData.TenantA, 1, "key-1", succeeded: true));

        await SaveAsync(
            TestData.CapabilityInvocation(TestData.TenantA, 2, "key-1"),
            TestData.CapabilityInvocation(TestData.TenantA, 3, "key-1", succeeded: true, capabilityName: "HumanAction.Complete"),
            TestData.CapabilityInvocation(TestData.TenantB, 4, "key-1", succeeded: true));
    }

    [Fact]
    public async Task Commit_RequestedInvocationSucceedingWithTakenKey_ReturnsDuplicateKey()
    {
        await SaveAsync(TestData.CapabilityInvocation(TestData.TenantA, 1, "key-1", succeeded: true), TestData.CapabilityInvocation(TestData.TenantA, 2, "key-1"));
        await using PersistenceSession session = OpenSession();
        CapabilityInvocation? pending = await session.Get<ICapabilityInvocationRepository>().GetByIdAsync(new CapabilityInvocationId(TestData.Id(20002)), Cancellation);
        Assert.NotNull(pending);
        pending.Succeed("{}", TestData.Now);

        Result<CommitReceipt> commit = await session.CommitAsync(Cancellation);

        AssertDuplicateKey(commit);
    }
}
