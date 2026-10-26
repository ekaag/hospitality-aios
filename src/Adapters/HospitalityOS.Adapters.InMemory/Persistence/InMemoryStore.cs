using HospitalityOS.Ports.Persistence;
using HospitalityOS.SharedKernel.Results;
using HospitalityOS.SharedKernel.Tenancy;

namespace HospitalityOS.Adapters.InMemory.Persistence;

/// <summary>
/// The committed state shared by every scope: plain collections behind one lock, so a commit is atomic and
/// readers never see half of one. Singleton; one store per container.
/// </summary>
internal sealed class InMemoryStore
{
    private readonly Lock gate = new();
    private readonly Dictionary<AggregateKey, StoredAggregate> aggregates = [];

    public StoredAggregate? Find(AggregateKey key)
    {
        lock (gate)
        {
            return aggregates.GetValueOrDefault(key);
        }
    }

    public IReadOnlyList<StoredAggregate> List(Type aggregateType, TenantId tenantId)
    {
        lock (gate)
        {
            return [.. aggregates.Values.Where(stored => stored.Key.AggregateType == aggregateType && stored.TenantId == tenantId)];
        }
    }

    /// <summary>
    /// Saves every write or none: checks versions and unique keys first, then applies.
    /// </summary>
    public Result Commit(IReadOnlyList<PendingWrite> writes)
    {
        lock (gate)
        {
            foreach (PendingWrite write in writes)
            {
                Result valid = CheckVersion(write);

                if (valid.IsFailure)
                {
                    return valid;
                }
            }

            Result unique = CheckUniqueKeys(writes);

            if (unique.IsFailure)
            {
                return unique;
            }

            foreach (PendingWrite write in writes)
            {
                aggregates[write.Aggregate.Key] = write.Aggregate;
            }

            return Result.Success();
        }
    }

    private Result CheckVersion(PendingWrite write)
    {
        AggregateKey key = write.Aggregate.Key;
        StoredAggregate? current = aggregates.GetValueOrDefault(key);

        if (write.IsNew)
        {
            return current is null ? Result.Success() : PersistenceErrors.DuplicateKey(key.AggregateType.Name, "id");
        }

        return current is not null && current.Version == write.ExpectedVersion
            ? Result.Success()
            : PersistenceErrors.ConcurrencyConflict(key.AggregateType.Name, key.Id);
    }

    private Result CheckUniqueKeys(IReadOnlyList<PendingWrite> writes)
    {
        HashSet<AggregateKey> written = [.. writes.Select(write => write.Aggregate.Key)];

        foreach (IGrouping<Type, PendingWrite> sameType in writes.GroupBy(write => write.Aggregate.Key.AggregateType))
        {
            foreach (UniqueKey uniqueKey in UniqueKeys.For(sameType.Key))
            {
                // The state after the commit: unchanged stored aggregates plus everything being written.
                IEnumerable<StoredAggregate> after = aggregates.Values
                    .Where(stored => stored.Key.AggregateType == sameType.Key && !written.Contains(stored.Key))
                    .Concat(sameType.Select(write => write.Aggregate));

                bool duplicated = after
                    .Select(stored => uniqueKey.Select(stored.Snapshot))
                    .OfType<object>()
                    .GroupBy(value => value)
                    .Any(group => group.Skip(1).Any());

                if (duplicated)
                {
                    return PersistenceErrors.DuplicateKey(sameType.Key.Name, uniqueKey.Name);
                }
            }
        }

        return Result.Success();
    }
}
