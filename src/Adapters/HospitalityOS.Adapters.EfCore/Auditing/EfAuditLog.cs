using HospitalityOS.Adapters.EfCore.Persistence;
using HospitalityOS.Ports.Auditing;
using HospitalityOS.Ports.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace HospitalityOS.Adapters.EfCore.Auditing;

/// <summary>
/// Appends by adding rows to the scope's context (saved by the unit of work); reads are untracked and filtered to the
/// current tenant by the context. Nothing here updates or deletes.
/// </summary>
internal sealed class EfAuditLog(HospitalityDbContext context, ICurrentTenant currentTenant) : IAuditLog
{
    public void Append(AuditRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        if (record.TenantId != currentTenant.TenantId)
        {
            // Writing into another tenant's audit log is a programming error, never an expected failure (P10).
            throw new InvalidOperationException($"Cannot audit {record.Action} for another tenant.");
        }

        context.Add(AuditRecordRow.From(record));
    }

    public async Task<IReadOnlyList<AuditEntry>> ReadAsync(AuditQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentOutOfRangeException.ThrowIfNegative(query.AfterPosition);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(query.Limit);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(query.Limit, AuditQuery.MaxLimit);

        IQueryable<AuditRecordRow> rows = context.Set<AuditRecordRow>().AsNoTracking().Where(row => row.Position > query.AfterPosition);

        if (query.CorrelationId is { } correlationId)
        {
            rows = rows.Where(row => row.CorrelationId == correlationId);
        }

        if (query.TargetType is { } targetType)
        {
            rows = rows.Where(row => row.TargetType == targetType);
        }

        if (query.TargetId is { } targetId)
        {
            rows = rows.Where(row => row.TargetId == targetId);
        }

        if (query.OccurredFrom is { } from)
        {
            rows = rows.Where(row => row.OccurredAt >= from);
        }

        if (query.OccurredBefore is { } before)
        {
            rows = rows.Where(row => row.OccurredAt < before);
        }

        List<AuditRecordRow> found = await rows.OrderBy(row => row.Position).Take(query.Limit).ToListAsync(cancellationToken).ConfigureAwait(false);

        return [.. found.Select(row => row.ToEntry())];
    }
}
