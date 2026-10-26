using HospitalityOS.Ports.Auditing;
using HospitalityOS.Ports.Bindings;
using HospitalityOS.Ports.Execution;
using HospitalityOS.Ports.Ids;
using HospitalityOS.Ports.Tenancy;

namespace HospitalityOS.Application.Auditing;

internal sealed class AuditTrail(
    IAuditLog auditLog,
    ICurrentTenant currentTenant,
    OperationContext operation,
    IBindingResolver<IIdGenerator> idGenerators,
    TimeProvider timeProvider) : IAuditTrail
{
    public void Record(string action, AuditOutcome outcome, AuditTarget? target = null, string? detail = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(action);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(action.Length, AuditRecord.MaxActionLength, nameof(action));
        ArgumentOutOfRangeException.ThrowIfGreaterThan(target?.Type.Length ?? 0, AuditRecord.MaxTargetTypeLength, nameof(target));
        ArgumentOutOfRangeException.ThrowIfGreaterThan(target?.Id.Length ?? 0, AuditRecord.MaxTargetIdLength, nameof(target));
        ArgumentOutOfRangeException.ThrowIfGreaterThan(operation.Actor.Id?.Length ?? 0, AuditRecord.MaxActorIdLength, nameof(operation));

        // Truncated rather than rejected: an over-long explanation must not stop the action from being audited.
        string? boundedDetail = detail is { Length: > AuditRecord.MaxDetailLength } ? detail[..AuditRecord.MaxDetailLength] : detail;

        auditLog.Append(new AuditRecord(
            idGenerators.Resolve().NewId(),
            currentTenant.TenantId,
            target?.PropertyId,
            action,
            outcome,
            operation.Actor,
            target?.Type,
            target?.Id,
            operation.CorrelationId,
            operation.CausationId,
            timeProvider.GetUtcNow(),
            boundedDetail));
    }
}
