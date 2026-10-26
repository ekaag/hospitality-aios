using HospitalityOS.Contracts.Events;
using HospitalityOS.Eventing.Serialization;
using HospitalityOS.Ports.Eventing;
using HospitalityOS.Ports.Persistence;

namespace HospitalityOS.Eventing.Envelopes;

/// <summary>
/// What persistence calls on commit: wraps each committed domain event in its envelope and serializes it, so the
/// event store and outbox hold the canonical, versioned JSON (P1). Scoped, sharing the scope's origin.
/// </summary>
internal sealed class EventMessageFactory(EventEnvelopeFactory envelopes, EventSerializer serializer, ScopedEventOrigin origin) : IEventMessageFactory
{
    public EventMessage Create(CommittedEvent committed)
    {
        EventEnvelope envelope = envelopes.Create(committed, origin.Current);

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
