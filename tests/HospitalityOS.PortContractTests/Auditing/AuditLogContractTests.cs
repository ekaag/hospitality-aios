using HospitalityOS.PortContractTests.Persistence;
using HospitalityOS.PortContractTests.Persistence.Support;
using HospitalityOS.Ports.Auditing;
using HospitalityOS.Ports.Persistence;
using HospitalityOS.SharedKernel.Domain;
using HospitalityOS.SharedKernel.Execution;
using HospitalityOS.SharedKernel.Results;
using HospitalityOS.SharedKernel.Tenancy;
using Xunit;

namespace HospitalityOS.PortContractTests.Auditing;

/// <summary>
/// The append-only audit log (P10): records are saved only with the unit of work's commit, never twice, and each
/// tenant reads only its own.
/// </summary>
public abstract class AuditLogContractTests : PersistenceContractTests
{
    private static readonly Guid flowA = TestData.Id(910_001);
    private static readonly Guid flowB = TestData.Id(910_002);

    [Fact]
    public async Task Read_AfterCommit_ReturnsRecordsInOrder()
    {
        AuditRecord[] appended = [Record(1), Record(2), Record(3)];
        await AppendAndCommitAsync(TestData.TenantA, appended);

        IReadOnlyList<AuditEntry> read = await ReadAsync(TestData.TenantA, new AuditQuery());

        Assert.Equal(appended, read.Select(entry => entry.Record));
        Assert.True(read.Zip(read.Skip(1)).All(pair => pair.First.Position < pair.Second.Position));
    }

    [Fact]
    public async Task Append_WithoutCommit_SavesNothing()
    {
        await using (PersistenceSession session = OpenSession())
        {
            session.Get<IAuditLog>().Append(Record(1));
        }

        Assert.Empty(await ReadAsync(TestData.TenantA, new AuditQuery()));
    }

    [Fact]
    public async Task Commit_WhenTheAuditedChangeFails_SavesNoRecord()
    {
        AggregateCase organizations = AggregateCases.Get("Organization");
        await SaveAsync(organizations.Create(TestData.TenantA, 1));
        await using PersistenceSession session = OpenSession();
        await organizations.AddAsync(session, organizations.Create(TestData.TenantA, 1), Cancellation);
        session.Get<IAuditLog>().Append(Record(1));

        Result<CommitReceipt> commit = await session.CommitAsync(Cancellation);

        Assert.True(commit.IsFailure);
        Assert.Empty(await ReadAsync(TestData.TenantA, new AuditQuery()));
    }

    [Fact]
    public async Task Commit_AuditIdAlreadySaved_ReturnsDuplicateKey()
    {
        await AppendAndCommitAsync(TestData.TenantA, Record(1));
        await using PersistenceSession session = OpenSession();
        session.Get<IAuditLog>().Append(Record(1) with { Detail = "rewritten" });

        Result<CommitReceipt> commit = await session.CommitAsync(Cancellation);

        Assert.Equal(PersistenceErrors.DuplicateKeyCode, commit.Error.Code);
        Assert.Null(Assert.Single(await ReadAsync(TestData.TenantA, new AuditQuery())).Record.Detail);
    }

    [Fact]
    public async Task Read_ReturnsOnlyTheCurrentTenantsRecords()
    {
        await AppendAndCommitAsync(TestData.TenantA, Record(1));
        await AppendAndCommitAsync(TestData.TenantB, Record(2, TestData.TenantB));

        IReadOnlyList<AuditEntry> tenantA = await ReadAsync(TestData.TenantA, new AuditQuery());
        IReadOnlyList<AuditEntry> tenantB = await ReadAsync(TestData.TenantB, new AuditQuery());

        Assert.Equal([Record(1)], tenantA.Select(entry => entry.Record));
        Assert.Equal([Record(2, TestData.TenantB)], tenantB.Select(entry => entry.Record));
    }

    [Fact]
    public async Task Append_RecordOfAnotherTenant_Throws()
    {
        await using PersistenceSession session = OpenSession(TestData.TenantB);

        Assert.Throws<InvalidOperationException>(() => session.Get<IAuditLog>().Append(Record(1)));
    }

    [Fact]
    public async Task Read_WithFilters_ReturnsOnlyMatchingRecords()
    {
        AuditRecord first = Record(1, correlationId: flowA, targetId: "412");
        AuditRecord second = Record(2, correlationId: flowB, targetId: "412");
        AuditRecord third = Record(3, correlationId: flowB, targetId: "415", occurredAt: TestData.Now.AddHours(1));
        await AppendAndCommitAsync(TestData.TenantA, first, second, third);

        IReadOnlyList<AuditEntry> byFlow = await ReadAsync(TestData.TenantA, new AuditQuery(CorrelationId: flowB));
        IReadOnlyList<AuditEntry> byTarget = await ReadAsync(TestData.TenantA, new AuditQuery(TargetType: "AccommodationUnit", TargetId: "412"));
        IReadOnlyList<AuditEntry> byTime = await ReadAsync(TestData.TenantA, new AuditQuery(OccurredFrom: TestData.Now.AddSeconds(2), OccurredBefore: TestData.Now.AddMinutes(30)));

        Assert.Equal([second, third], byFlow.Select(entry => entry.Record));
        Assert.Equal([first, second], byTarget.Select(entry => entry.Record));
        Assert.Equal([second], byTime.Select(entry => entry.Record));
    }

    [Fact]
    public async Task Read_AfterPosition_ContinuesWhereThePreviousPageEnded()
    {
        AuditRecord[] appended = [.. Enumerable.Range(1, 5).Select(seed => Record(seed))];
        await AppendAndCommitAsync(TestData.TenantA, appended);

        IReadOnlyList<AuditEntry> first = await ReadAsync(TestData.TenantA, new AuditQuery(Limit: 2));
        IReadOnlyList<AuditEntry> second = await ReadAsync(TestData.TenantA, new AuditQuery(AfterPosition: first[^1].Position, Limit: 2));

        Assert.Equal(appended[..2], first.Select(entry => entry.Record));
        Assert.Equal(appended[2..4], second.Select(entry => entry.Record));
    }

    [Fact]
    public async Task Commit_WithAggregate_SavesBothTogether()
    {
        AggregateCase organizations = AggregateCases.Get("Organization");
        IAggregateRoot organization = organizations.Create(TestData.TenantA, 1);

        await using (PersistenceSession session = OpenSession())
        {
            await organizations.AddAsync(session, organization, Cancellation);
            session.Get<IAuditLog>().Append(Record(1));
            Assert.True((await session.CommitAsync(Cancellation)).IsSuccess);
        }

        await using PersistenceSession reader = OpenSession();

        Assert.NotNull(await organizations.GetAsync(reader, organizations.IdOf(organization), Cancellation));
        Assert.Single(await reader.Get<IAuditLog>().ReadAsync(new AuditQuery(), Cancellation));
    }

    private static AuditRecord Record(int seed, TenantId? tenantId = null, Guid? correlationId = null, string? targetId = null, DateTimeOffset? occurredAt = null) => new(
        TestData.Id(920_000 + seed),
        tenantId ?? TestData.TenantA,
        TestData.PropertyId,
        "accommodation_unit.renumber",
        AuditOutcome.Succeeded,
        new Actor(ActorType.Staff, "maria"),
        "AccommodationUnit",
        targetId ?? "412",
        correlationId ?? flowA,
        TestData.Id(930_000 + seed),
        occurredAt ?? TestData.Now.AddSeconds(seed),
        null);

    private async Task AppendAndCommitAsync(TenantId tenantId, params AuditRecord[] records)
    {
        await using PersistenceSession session = OpenSession(tenantId);

        foreach (AuditRecord record in records)
        {
            session.Get<IAuditLog>().Append(record);
        }

        Assert.True((await session.CommitAsync(Cancellation)).IsSuccess);
    }

    private async Task<IReadOnlyList<AuditEntry>> ReadAsync(TenantId tenantId, AuditQuery query)
    {
        await using PersistenceSession session = OpenSession(tenantId);

        return await session.Get<IAuditLog>().ReadAsync(query, Cancellation);
    }
}
