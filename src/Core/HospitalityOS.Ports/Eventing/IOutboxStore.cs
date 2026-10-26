namespace HospitalityOS.Ports.Eventing;

/// <summary>
/// The queue of recorded events waiting to be published, as the dispatcher (C5) sees it. Events enter it through
/// <see cref="IEventStore.Append"/>. Every operation here takes effect immediately, on its own, and never saves
/// anything else the scope has pending. Scoped.
/// </summary>
/// <remarks>
/// Delivery is at-least-once: a dispatcher that crashes after publishing but before <see cref="MarkDispatchedAsync"/>
/// publishes again once its lease expires, so consumers de-duplicate on the event ID (P8). Several dispatchers may
/// run at once; a lease keeps two from working on the same event at the same time.
/// </remarks>
public interface IOutboxStore
{
    /// <summary>
    /// Claims up to <paramref name="maxCount"/> events that are due, oldest first, by leasing each until
    /// <paramref name="now"/> + <paramref name="lease"/>. An event is due when its next attempt time has come, it is
    /// not leased (or its lease expired), and it is not dead-lettered.
    /// </summary>
    /// <param name="now">The current time (UTC).</param>
    /// <param name="maxCount">The most events to claim; at least 1.</param>
    /// <param name="lease">How long the claim lasts before another dispatcher may take the event; positive.</param>
    /// <param name="cancellationToken">Cancels the claim.</param>
    /// <returns>The claimed events, oldest first.</returns>
    Task<IReadOnlyList<OutboxEntry>> ClaimDueAsync(DateTimeOffset now, int maxCount, TimeSpan lease, CancellationToken cancellationToken);

    /// <summary>
    /// Removes a published event from the queue. Marking an event that is no longer queued is a no-op.
    /// </summary>
    Task MarkDispatchedAsync(Guid eventId, CancellationToken cancellationToken);

    /// <summary>
    /// Records a failed publish attempt and releases the lease; the event is due again at <paramref name="retryAt"/>.
    /// </summary>
    Task ScheduleRetryAsync(Guid eventId, DateTimeOffset retryAt, string error, CancellationToken cancellationToken);

    /// <summary>
    /// Stops retrying an event that cannot be published; it stays visible for inspection and is never claimed again.
    /// </summary>
    Task DeadLetterAsync(Guid eventId, DateTimeOffset at, string error, CancellationToken cancellationToken);
}
