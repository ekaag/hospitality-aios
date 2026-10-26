using HospitalityOS.Ports.Eventing;
using HospitalityOS.Ports.Persistence;
using HospitalityOS.SharedKernel.Domain;
using HospitalityOS.SharedKernel.Results;
using HospitalityOS.SharedKernel.Tenancy;

namespace HospitalityOS.Adapters.InMemory.Persistence;

/// <summary>
/// Tracks what one scope added and loaded (one instance per aggregate) and the event and inbox writes it staged,
/// and commits all of it to the <see cref="InMemoryStore"/> at once, recording every committed domain event in the event
/// store and outbox. Scoped and not thread-safe, like a database session.
/// </summary>
internal sealed class InMemoryUnitOfWork(InMemoryStore store, IEventMessageFactory messageFactory) : IUnitOfWork
{
    private readonly Dictionary<AggregateKey, TrackedAggregate> trackedByKey = [];
    private readonly List<TrackedAggregate> trackedInOrder = [];
    private readonly List<EventMessage> stagedEvents = [];
    private readonly List<InboxKey> stagedInboxRecords = [];

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

    public void StageEvent(EventMessage message) => stagedEvents.Add(message);

    public void StageInboxRecord(InboxKey key) => stagedInboxRecords.Add(key);

    public Task<Result<CommitReceipt>> CommitAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        List<TrackedAggregate> changed = [.. trackedInOrder.Where(tracked => tracked.HasChanges)];
        List<CommittedEvent> events = [.. changed.SelectMany(PendingEvents)];

        // The recorded messages exist only in this batch, so a failed commit records nothing and a retry records each event once.
        CommitBatch batch = new([.. changed.Select(ToPendingWrite)], [.. stagedEvents, .. events.Select(messageFactory.Create)], [.. stagedInboxRecords]);

        if (batch.IsEmpty)
        {
            return Task.FromResult<Result<CommitReceipt>>(CommitReceipt.Empty);
        }

        Result committed = store.Commit(batch);

        if (committed.IsFailure)
        {
            return Task.FromResult<Result<CommitReceipt>>(committed.Error);
        }

        stagedEvents.Clear();
        stagedInboxRecords.Clear();

        // Only after the store accepted everything, so a failed commit leaves the events (and staged writes) in place for a retry.
        foreach (TrackedAggregate tracked in changed)
        {
            tracked.Aggregate.DequeueDomainEvents();
            tracked.CommittedVersion = tracked.Aggregate.Version;
        }

        return Task.FromResult<Result<CommitReceipt>>(new CommitReceipt(events));
    }

    private static IEnumerable<CommittedEvent> PendingEvents(TrackedAggregate tracked)
    {
        IReadOnlyList<IDomainEvent> raised = tracked.Aggregate.DomainEvents;
        long firstVersion = tracked.Aggregate.Version - raised.Count + 1;

        return raised.Select((domainEvent, index) => new CommittedEvent(
            tracked.TenantId,
            (tracked.Aggregate as IPropertyScoped)?.PropertyId,
            tracked.Key.AggregateType.Name,
            tracked.Key.Id,
            firstVersion + index,
            domainEvent));
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
