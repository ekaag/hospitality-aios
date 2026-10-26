using HospitalityOS.Ports.Persistence;

namespace HospitalityOS.Ports.Eventing;

/// <summary>
/// Turns a domain event, as it is being committed with its aggregate, into the message recorded in the event store
/// and queued in the outbox in that same transaction (P1). Persistence adapters call it during
/// <see cref="IUnitOfWork.CommitAsync"/>; the event backbone implements it (envelope, published contract,
/// serialization, correlation). Scoped: one per request or message, so all its events share a correlation.
/// </summary>
public interface IEventMessageFactory
{
    /// <summary>
    /// Creates the message for one committed event, with a new event ID.
    /// </summary>
    /// <param name="committed">The event and the aggregate that raised it.</param>
    /// <returns>The message to record and publish.</returns>
    EventMessage Create(CommittedEvent committed);
}
