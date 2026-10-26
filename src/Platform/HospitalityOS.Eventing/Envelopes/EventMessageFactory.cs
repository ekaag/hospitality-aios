using HospitalityOS.Contracts.Events;
using HospitalityOS.Eventing.Serialization;
using HospitalityOS.Ports.Eventing;
using HospitalityOS.Ports.Execution;
using HospitalityOS.Ports.Persistence;

namespace HospitalityOS.Eventing.Envelopes;

/// <summary>
/// What persistence calls on commit: wraps each committed domain event in its envelope and serializes it, so the
/// event store and outbox hold the canonical, versioned JSON (P1), stamped with the scope's correlation, causation, and
/// actor (P11). Scoped.
/// </summary>
internal sealed class EventMessageFactory(EventEnvelopeFactory envelopes, EventSerializer serializer, OperationContext operation) : IEventMessageFactory
{
    public EventMessage Create(CommittedEvent committed)
    {
        EventOrigin origin = new(operation.CorrelationId, operation.CausationId, operation.Actor.Type, operation.Actor.Id);
        EventEnvelope envelope = envelopes.Create(committed, origin);

        return new EventMessage(
            envelope.EventId,
            envelope.EventType,
            envelope.EventVersion,
            envelope.TenantId,
            envelope.AggregateType,
            envelope.AggregateId,
            envelope.CorrelationId,
            envelope.RecordedAt,
            serializer.Serialize(envelope));
    }
}
