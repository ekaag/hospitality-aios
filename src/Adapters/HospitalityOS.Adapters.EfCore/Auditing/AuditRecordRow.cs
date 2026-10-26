using HospitalityOS.Ports.Auditing;
using HospitalityOS.SharedKernel.Execution;
using HospitalityOS.SharedKernel.Ids;
using HospitalityOS.SharedKernel.Tenancy;

namespace HospitalityOS.Adapters.EfCore.Auditing;

/// <summary>
/// A row of the append-only audit log. Tenant-scoped, so the context's tenant filter applies to every read.
/// <see cref="Position"/> is assigned by the database.
/// </summary>
internal sealed class AuditRecordRow : ITenantScoped
{
    public long Position { get; set; }

    public Guid AuditId { get; set; }

    public TenantId TenantId { get; set; }

    public PropertyId? PropertyId { get; set; }

    public string Action { get; set; } = string.Empty;

    public AuditOutcome Outcome { get; set; }

    public ActorType ActorType { get; set; }

    public string? ActorId { get; set; }

    public string? TargetType { get; set; }

    public string? TargetId { get; set; }

    public Guid CorrelationId { get; set; }

    public Guid? CausationId { get; set; }

    public DateTimeOffset OccurredAt { get; set; }

    public string? Detail { get; set; }

    public static AuditRecordRow From(AuditRecord record) => new()
    {
        AuditId = record.AuditId,
        TenantId = record.TenantId,
        PropertyId = record.PropertyId,
        Action = record.Action,
        Outcome = record.Outcome,
        ActorType = record.Actor.Type,
        ActorId = record.Actor.Id,
        TargetType = record.TargetType,
        TargetId = record.TargetId,
        CorrelationId = record.CorrelationId,
        CausationId = record.CausationId,
        OccurredAt = record.OccurredAt,
        Detail = record.Detail,
    };

    public AuditEntry ToEntry() => new(
        Position,
        new AuditRecord(AuditId, TenantId, PropertyId, Action, Outcome, new Actor(ActorType, ActorId), TargetType, TargetId, CorrelationId, CausationId, OccurredAt, Detail));
}
