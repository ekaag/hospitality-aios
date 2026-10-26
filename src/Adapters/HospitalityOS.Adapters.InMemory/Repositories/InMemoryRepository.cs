using HospitalityOS.Adapters.InMemory.Persistence;
using HospitalityOS.Ports.Tenancy;
using HospitalityOS.SharedKernel.Domain;
using HospitalityOS.SharedKernel.Ids;
using HospitalityOS.SharedKernel.Tenancy;

namespace HospitalityOS.Adapters.InMemory.Repositories;

/// <summary>
/// What every in-memory repository shares: reads only the current tenant's committed aggregates, hands out
/// copies, returns the scope's tracked instance when there is one, and tracks adds for the unit of work.
/// </summary>
internal abstract class InMemoryRepository<TAggregate, TId>(InMemoryStore store, InMemoryUnitOfWork unitOfWork, ICurrentTenant currentTenant)
    where TAggregate : AggregateRoot<TId>, ITenantScoped
    where TId : struct, IEquatable<TId>, ITypedId
{
    public Task<TAggregate?> GetByIdAsync(TId id, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        AggregateKey key = KeyOf(id);

        if (unitOfWork.FindTracked(key) is TAggregate tracked)
        {
            return Task.FromResult<TAggregate?>(tracked);
        }

        StoredAggregate? stored = store.Find(key);

        return Task.FromResult(stored is null || stored.TenantId != currentTenant.TenantId ? null : Materialize(stored));
    }

    public Task AddAsync(TAggregate aggregate, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(aggregate);
        cancellationToken.ThrowIfCancellationRequested();

        if (aggregate.TenantId != currentTenant.TenantId)
        {
            // A caller holding another tenant's aggregate is a programming error, never an expected failure (P10).
            throw new InvalidOperationException($"Cannot add {typeof(TAggregate).Name} {aggregate.Id} of another tenant.");
        }

        unitOfWork.TrackAdded(KeyOf(aggregate.Id), aggregate.TenantId, aggregate);

        return Task.CompletedTask;
    }

    /// <summary>
    /// Runs <paramref name="query"/> over the current tenant's committed aggregates, as a database query would,
    /// then returns tracked instances in the query's order.
    /// </summary>
    protected Task<IReadOnlyList<TAggregate>> QueryAsync(
        Func<IEnumerable<TAggregate>, IEnumerable<TAggregate>> query,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        Dictionary<TAggregate, StoredAggregate> committed = store
            .List(typeof(TAggregate), currentTenant.TenantId)
            .ToDictionary<StoredAggregate, TAggregate, StoredAggregate>(stored => (TAggregate)stored.Snapshot, stored => stored, ReferenceEqualityComparer.Instance);

        IReadOnlyList<TAggregate> results = [.. query(committed.Keys).Select(snapshot => Materialize(committed[snapshot]))];

        return Task.FromResult(results);
    }

    protected async Task<TAggregate?> FindFirstAsync(Func<TAggregate, bool> predicate, CancellationToken cancellationToken)
    {
        IReadOnlyList<TAggregate> found = await QueryAsync(aggregates => aggregates.Where(predicate).Take(1), cancellationToken).ConfigureAwait(false);

        return found.Count == 0 ? null : found[0];
    }

    private static AggregateKey KeyOf(TId id) => new(typeof(TAggregate), id.Value);

    private TAggregate Materialize(StoredAggregate stored)
    {
        if (unitOfWork.FindTracked(stored.Key) is TAggregate tracked)
        {
            return tracked;
        }

        TAggregate copy = (TAggregate)SnapshotCopier.Copy(stored.Snapshot);
        unitOfWork.TrackLoaded(stored.Key, stored.TenantId, copy);

        return copy;
    }
}
