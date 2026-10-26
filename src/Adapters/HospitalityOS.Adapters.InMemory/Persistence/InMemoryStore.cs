using HospitalityOS.Ports.Eventing;
using HospitalityOS.Ports.Persistence;
using HospitalityOS.SharedKernel.Results;
using HospitalityOS.SharedKernel.Tenancy;

namespace HospitalityOS.Adapters.InMemory.Persistence;

/// <summary>
/// The committed state shared by every scope: aggregates, the event log, the outbox, and the inbox, as plain
/// collections behind one lock, so a commit is atomic and readers never see half of one. Singleton; one store per
/// container.
/// </summary>
internal sealed class InMemoryStore
{
    private readonly Lock gate = new();
    private readonly Dictionary<AggregateKey, StoredAggregate> aggregates = [];
    private readonly List<StoredEvent> events = [];
    private readonly HashSet<Guid> eventIds = [];
    private readonly Dictionary<Guid, OutboxRow> outbox = [];
    private readonly HashSet<InboxKey> inbox = [];

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
    /// Saves the whole batch or none of it: checks versions and unique keys first, then applies.
    /// </summary>
    public Result Commit(CommitBatch batch)
    {
        lock (gate)
        {
            Result valid = Validate(batch);

            if (valid.IsFailure)
            {
                return valid;
            }

            foreach (PendingWrite write in batch.Aggregates)
            {
                aggregates[write.Aggregate.Key] = write.Aggregate;
            }

            foreach (EventMessage message in batch.Events)
            {
                StoredEvent stored = new(events.Count + 1, message);
                events.Add(stored);
                eventIds.Add(message.EventId);
                outbox.Add(message.EventId, new OutboxRow(stored.Position, message));
            }

            inbox.UnionWith(batch.InboxRecords);

            return Result.Success();
        }
    }

    public IReadOnlyList<StoredEvent> ReadEvents(EventStoreQuery query)
    {
        lock (gate)
        {
            return [.. events
                .Where(stored => stored.Position > query.AfterPosition)
                .Where(stored => query.TenantId is not { } tenant || stored.Message.TenantId == tenant)
                .Where(stored => query.CorrelationId is not { } correlation || stored.Message.CorrelationId == correlation)
                .Where(stored => query.RecordedFrom is not { } from || stored.Message.RecordedAt >= from)
                .Where(stored => query.RecordedBefore is not { } before || stored.Message.RecordedAt < before)
                .Take(query.Limit)];
        }
    }

    public IReadOnlyList<OutboxEntry> ClaimDue(DateTimeOffset now, int maxCount, TimeSpan lease)
    {
        lock (gate)
        {
            List<OutboxRow> claimed = [.. outbox.Values.Where(row => row.IsDueAt(now)).OrderBy(row => row.Position).Take(maxCount)];

            foreach (OutboxRow row in claimed)
            {
                row.LeaseUntil = now + lease;
            }

            return [.. claimed.Select(row => new OutboxEntry(row.Message, row.Attempts, row.LastError))];
        }
    }

    public void MarkDispatched(Guid eventId)
    {
        lock (gate)
        {
            outbox.Remove(eventId);
        }
    }

    public void ScheduleRetry(Guid eventId, DateTimeOffset retryAt, string error)
    {
        lock (gate)
        {
            if (outbox.TryGetValue(eventId, out OutboxRow? row))
            {
                row.Attempts++;
                row.LastError = error;
                row.NextAttemptAt = retryAt;
                row.LeaseUntil = null;
            }
        }
    }

    public void DeadLetter(Guid eventId, DateTimeOffset at, string error)
    {
        lock (gate)
        {
            if (outbox.TryGetValue(eventId, out OutboxRow? row))
            {
                row.LastError = error;
                row.DeadLetteredAt = at;
                row.LeaseUntil = null;
            }
        }
    }

    public bool HasProcessed(InboxKey key)
    {
        lock (gate)
        {
            return inbox.Contains(key);
        }
    }

    private Result Validate(CommitBatch batch)
    {
        foreach (PendingWrite write in batch.Aggregates)
        {
            Result version = CheckVersion(write);

            if (version.IsFailure)
            {
                return version;
            }
        }

        Result unique = CheckUniqueKeys(batch.Aggregates);

        if (unique.IsFailure)
        {
            return unique;
        }

        HashSet<Guid> newEventIds = [];

        if (batch.Events.Any(message => eventIds.Contains(message.EventId) || !newEventIds.Add(message.EventId)))
        {
            return PersistenceErrors.DuplicateKey("event", "eventId");
        }

        HashSet<InboxKey> newInboxKeys = [];

        return batch.InboxRecords.Any(key => inbox.Contains(key) || !newInboxKeys.Add(key))
            ? PersistenceErrors.DuplicateKey("inbox record", "handler and eventId")
            : Result.Success();
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
