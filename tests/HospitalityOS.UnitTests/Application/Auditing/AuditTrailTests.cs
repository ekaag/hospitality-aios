using HospitalityOS.Application.Auditing;
using HospitalityOS.Ports.Auditing;
using HospitalityOS.Ports.Execution;
using HospitalityOS.Ports.Tenancy;
using HospitalityOS.SharedKernel.Execution;
using HospitalityOS.SharedKernel.Ids;
using HospitalityOS.SharedKernel.Tenancy;
using HospitalityOS.UnitTests.Eventing.TestDoubles;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace HospitalityOS.UnitTests.Application.Auditing;

public sealed class AuditTrailTests
{
    private static readonly Guid auditId = Guid.Parse("01890a5d-ac96-774b-bcce-000000000001");
    private static readonly Guid flow = Guid.Parse("01890a5d-ac96-774b-bcce-000000000002");
    private static readonly Guid cause = Guid.Parse("01890a5d-ac96-774b-bcce-000000000003");
    private static readonly TenantId tenantId = new(Guid.Parse("01890a5d-ac96-774b-bcce-000000000004"));
    private static readonly PropertyId propertyId = new(Guid.Parse("01890a5d-ac96-774b-bcce-000000000005"));
    private static readonly DateTimeOffset now = new(2026, 10, 26, 9, 0, 0, TimeSpan.Zero);

    private readonly RecordingAuditLog log = new();
    private readonly AuditTrail trail;

    public AuditTrailTests()
    {
        TenantContext tenant = new();
        tenant.Set(tenantId);
        OperationContext operation = new(new FixedIdGenerator(Guid.Parse("01890a5d-ac96-774b-bcce-000000000099")));
        operation.JoinFlow(flow, cause);
        operation.ActAs(new Actor(ActorType.Staff, "maria"));
        trail = new AuditTrail(log, tenant, operation, new FixedIdGenerator(auditId), new FakeTimeProvider(now));
    }

    [Fact]
    public void Record_FillsTenantActorFlowTimeAndId()
    {
        trail.Record("accommodation_unit.renumber", AuditOutcome.Succeeded, new AuditTarget("AccommodationUnit", "412", propertyId), "412 to 412A");

        AuditRecord record = Assert.Single(log.Appended);
        Assert.Equal(
            new AuditRecord(auditId, tenantId, propertyId, "accommodation_unit.renumber", AuditOutcome.Succeeded, new Actor(ActorType.Staff, "maria"), "AccommodationUnit", "412", flow, cause, now, "412 to 412A"),
            record);
    }

    [Fact]
    public void Record_WithoutTarget_LeavesTargetAndPropertyEmpty()
    {
        trail.Record("capability.denied", AuditOutcome.Denied);

        AuditRecord record = Assert.Single(log.Appended);
        Assert.Equal((null, null, (PropertyId?)null), (record.TargetType, record.TargetId, record.PropertyId));
    }

    [Fact]
    public void Record_OverlongDetail_IsTruncatedNotRejected()
    {
        trail.Record("policy.update", AuditOutcome.Failed, detail: new string('x', AuditRecord.MaxDetailLength + 10));

        Assert.Equal(AuditRecord.MaxDetailLength, Assert.Single(log.Appended).Detail!.Length);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Record_BlankAction_Throws(string action)
    {
        Assert.Throws<ArgumentException>(() => trail.Record(action, AuditOutcome.Succeeded));
        Assert.Empty(log.Appended);
    }

    [Fact]
    public void Record_OverlongAction_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => trail.Record(new string('a', AuditRecord.MaxActionLength + 1), AuditOutcome.Succeeded));
    }

    private sealed class RecordingAuditLog : IAuditLog
    {
        public List<AuditRecord> Appended { get; } = [];

        public void Append(AuditRecord record) => Appended.Add(record);

        public Task<IReadOnlyList<AuditEntry>> ReadAsync(AuditQuery query, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
