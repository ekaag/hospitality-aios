using HospitalityOS.Adapters.EfCore.Persistence;
using HospitalityOS.Domain.Capabilities.AITraces;
using HospitalityOS.Ports.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace HospitalityOS.Adapters.EfCore.Repositories;

internal sealed class EfAITraceRepository(HospitalityDbContext context, EfUnitOfWork unitOfWork, ICurrentTenant currentTenant)
    : EfRepository<AITrace, AITraceId>(context, unitOfWork, currentTenant), IAITraceRepository
{
    public Task<IReadOnlyList<AITrace>> ListAsync(AITraceQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(query.Limit);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(query.Limit, AITraceQuery.MaxLimit);

        string? after = query.After is { } cursor ? KeyColumns.Recency(cursor.CompletedAt, cursor.Id) : null;

        return QueryAsync(
            traces =>
            {
                IQueryable<AITrace> matching = traces;

                if (after is not null)
                {
#pragma warning disable CA1309 // Translated to SQL "<", which compares the ASCII key ordinally; EF Core cannot translate a StringComparison.
                    matching = matching.Where(trace => string.Compare(EF.Property<string>(trace, KeyColumns.RecencyKey), after) <= 0);
#pragma warning restore CA1309
                }

                if (query.PropertyId is { } property)
                {
                    matching = matching.Where(trace => trace.PropertyId == property);
                }

                if (query.CorrelationId is { } correlation)
                {
                    matching = matching.Where(trace => trace.CorrelationId == correlation);
                }

                if (query.Outcome is { } outcome)
                {
                    matching = matching.Where(trace => trace.Outcome == outcome);
                }

                if (query.CompletedFrom is { } from)
                {
                    matching = matching.Where(trace => trace.CompletedAt >= from);
                }

                if (query.CompletedBefore is { } before)
                {
                    matching = matching.Where(trace => trace.CompletedAt < before);
                }

                return matching.OrderByDescending(trace => EF.Property<string>(trace, KeyColumns.RecencyKey)).Take(query.Limit);
            },
            cancellationToken);
    }
}
