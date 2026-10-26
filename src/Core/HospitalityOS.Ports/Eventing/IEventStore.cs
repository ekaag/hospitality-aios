namespace HospitalityOS.Ports.Eventing;

/// <summary>
/// The durable, append-only log of every event (spec §8: replay support, auditability). Appending also queues the
/// event for publication in the outbox, in the same transaction, so nothing recorded is ever left unpublished and
/// nothing is published without being recorded (P1, P8). Scoped.
/// </summary>
/// <remarks>
/// Reads are system-level (dispatcher, replay, audit) and cross tenants unless the query names one; callers
/// authorize them. Recorded events never change and are never deleted (P10).
/// </remarks>
public interface IEventStore
{
    /// <summary>
    /// Stages an event; it is saved, and queued in the outbox, when the scope's <c>IUnitOfWork</c> commits, together
    /// with the aggregate changes that raised it. If the commit fails, the event is not recorded.
    /// </summary>
    /// <param name="message">The event.</param>
    void Append(EventMessage message);

    /// <summary>
    /// Reads recorded (committed) events in log order, oldest first.
    /// </summary>
    /// <param name="query">Which events, from where, and how many.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>Up to <see cref="EventStoreQuery.Limit"/> events; continue from the last one's <see cref="StoredEvent.Position"/>.</returns>
    Task<IReadOnlyList<StoredEvent>> ReadAsync(EventStoreQuery query, CancellationToken cancellationToken);
}
