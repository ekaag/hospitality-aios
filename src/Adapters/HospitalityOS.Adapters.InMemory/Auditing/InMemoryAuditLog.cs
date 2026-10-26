using HospitalityOS.Adapters.InMemory.Persistence;
using HospitalityOS.Ports.Auditing;
using HospitalityOS.Ports.Tenancy;

namespace HospitalityOS.Adapters.InMemory.Auditing;

internal sealed class InMemoryAuditLog(InMemoryStore store, InMemoryUnitOfWork unitOfWork, ICurrentTenant currentTenant) : IAuditLog
{
    public void Append(AuditRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        if (record.TenantId != currentTenant.TenantId)
        {
            // Writing into another tenant's audit log is a programming error, never an expected failure (P10).
            throw new InvalidOperationException($"Cannot audit {record.Action} for another tenant.");
        }

        unitOfWork.StageAuditRecord(record);
    }

    public Task<IReadOnlyList<AuditEntry>> ReadAsync(AuditQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentOutOfRangeException.ThrowIfNegative(query.AfterPosition);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(query.Limit);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(query.Limit, AuditQuery.MaxLimit);
        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult(store.ReadAudit(currentTenant.TenantId, query));
    }
}
