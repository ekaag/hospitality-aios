using HospitalityOS.Ports.Persistence;
using HospitalityOS.SharedKernel.Domain;
using HospitalityOS.SharedKernel.Results;
using HospitalityOS.SharedKernel.Tenancy;

namespace HospitalityOS.Adapters.InMemory.Persistence;

/// <summary>
/// Tracks what one scope added and loaded (one instance per aggregate), and commits the changes to the
/// <see cref="InMemoryStore"/>. Scoped and not thread-safe, like a database session.
/// </summary>
internal sealed class InMemoryUnitOfWork(InMemoryStore store) : IUnitOfWork
{
    private readonly Dictionary<AggregateKey, TrackedAggregate> trackedByKey = [];
    private readonly List<TrackedAggregate> trackedInOrder = [];

    public IAggregateRoot? FindTracked(AggregateKey key) => trackedByKey.GetValueOrDefault(key)?.Aggregate;

    public void TrackAdded(AggregateKey key, TenantId tenantId, IAggregateRoot aggregate)
    {
        if (trackedByKey.ContainsKey(key))
        {
            throw new InvalidOperationException($"{key.AggregateType.Name} {key.Id} is already tracked in this scope.");
        }

        Track(new TrackedAggregate(key, tenantId, aggregate, committedVersion: null));
    }

    public void TrackLoaded(AggregateKey key, TenantId tenantId, IAggregateRoot aggregate)
    {
        Track(new TrackedAggregate(key, tenantId, aggregate, aggregate.Version));
    }

    public Task<Result<CommitReceipt>> CommitAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        List<TrackedAggregate> changed = [.. trackedInOrder.Where(tracked => tracked.HasChanges)];

        if (changed.Count == 0)
        {
            return Task.FromResult<Result<CommitReceipt>>(CommitReceipt.Empty);
        }

        Result committed = store.Commit([.. changed.Select(ToPendingWrite)]);

        if (committed.IsFailure)
        {
            return Task.FromResult<Result<CommitReceipt>>(committed.Error);
        }

        // Only after the store accepted everything, so a failed commit leaves the events in place for a retry.
        List<CommittedEvent> events = [];

        foreach (TrackedAggregate tracked in changed)
        {
            IReadOnlyList<IDomainEvent> raised = tracked.Aggregate.DequeueDomainEvents();
            long firstVersion = tracked.Aggregate.Version - raised.Count + 1;

            events.AddRange(raised.Select((domainEvent, index) => new CommittedEvent(
                tracked.TenantId,
                (tracked.Aggregate as IPropertyScoped)?.PropertyId,
                tracked.Key.AggregateType.Name,
                tracked.Key.Id,
                firstVersion + index,
                domainEvent)));

            tracked.CommittedVersion = tracked.Aggregate.Version;
        }

        return Task.FromResult<Result<CommitReceipt>>(new CommitReceipt(events));
    }

    private static PendingWrite ToPendingWrite(TrackedAggregate tracked)
    {
        IAggregateRoot snapshot = SnapshotCopier.Copy(tracked.Aggregate);
        snapshot.DequeueDomainEvents();

        return new PendingWrite(new StoredAggregate(tracked.Key, tracked.TenantId, snapshot), tracked.CommittedVersion);
    }

    private void Track(TrackedAggregate tracked)
    {
        trackedByKey.Add(tracked.Key, tracked);
        trackedInOrder.Add(tracked);
    }
}
