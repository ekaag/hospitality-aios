using HospitalityOS.Adapters.InMemory.Persistence;
using HospitalityOS.Domain.Capabilities.AITraces;
using HospitalityOS.Ports.Tenancy;

namespace HospitalityOS.Adapters.InMemory.Repositories;

internal sealed class InMemoryAITraceRepository(InMemoryStore store, InMemoryUnitOfWork unitOfWork, ICurrentTenant currentTenant)
    : InMemoryRepository<AITrace, AITraceId>(store, unitOfWork, currentTenant), IAITraceRepository
{
    public Task<IReadOnlyList<AITrace>> ListAsync(AITraceQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(query.Limit);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(query.Limit, AITraceQuery.MaxLimit);

        return QueryAsync(
            traces => traces
                .Where(trace => query.After is not { } after || IsOlder(trace, after))
                .Where(trace => query.PropertyId is not { } property || trace.PropertyId == property)
                .Where(trace => query.CorrelationId is not { } correlation || trace.CorrelationId == correlation)
                .Where(trace => query.Outcome is not { } outcome || trace.Outcome == outcome)
                .Where(trace => query.CompletedFrom is not { } from || trace.CompletedAt >= from)
                .Where(trace => query.CompletedBefore is not { } before || trace.CompletedAt < before)
                .OrderByDescending(trace => trace.CompletedAt)
                .ThenBy(trace => trace.Id.Value.ToString("N"), StringComparer.Ordinal)
                .Take(query.Limit),
            cancellationToken);
    }

    // Newest first: by completion time, then by ID as the database orders it (lower-case hex, ordinal).
    private static bool IsOlder(AITrace trace, AITraceCursor after) =>
        trace.CompletedAt < after.CompletedAt
        || (trace.CompletedAt == after.CompletedAt && string.CompareOrdinal(trace.Id.Value.ToString("N"), after.Id.Value.ToString("N")) < 0);
}
