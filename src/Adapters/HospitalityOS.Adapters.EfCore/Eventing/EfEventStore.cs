using HospitalityOS.Adapters.EfCore.Persistence;
using HospitalityOS.Ports.Eventing;
using Microsoft.EntityFrameworkCore;

namespace HospitalityOS.Adapters.EfCore.Eventing;

/// <summary>
/// Appends by adding log and outbox rows to the scope's context, so the unit of work saves them in the same
/// transaction as the aggregates. Reads are untracked and see committed rows only.
/// </summary>
internal sealed class EfEventStore(HospitalityDbContext context) : IEventStore
{
    public void Append(EventMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);

        context.Add(EventRecord.From(message));
        context.Add(OutboxRecord.From(message));
    }

    public async Task<IReadOnlyList<StoredEvent>> ReadAsync(EventStoreQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentOutOfRangeException.ThrowIfNegative(query.AfterPosition);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(query.Limit);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(query.Limit, EventStoreQuery.MaxLimit);

        IQueryable<EventRecord> records = context.Set<EventRecord>().AsNoTracking().Where(record => record.Position > query.AfterPosition);

        if (query.TenantId is { } tenantId)
        {
            records = records.Where(record => record.TenantId == tenantId);
        }

        if (query.CorrelationId is { } correlationId)
        {
            records = records.Where(record => record.CorrelationId == correlationId);
        }

        if (query.RecordedFrom is { } from)
        {
            records = records.Where(record => record.RecordedAt >= from);
        }

        if (query.RecordedBefore is { } before)
        {
            records = records.Where(record => record.RecordedAt < before);
        }

        List<EventRecord> found = await records.OrderBy(record => record.Position).Take(query.Limit).ToListAsync(cancellationToken).ConfigureAwait(false);

        return [.. found.Select(record => new StoredEvent(record.Position, record.ToMessage()))];
    }
}
