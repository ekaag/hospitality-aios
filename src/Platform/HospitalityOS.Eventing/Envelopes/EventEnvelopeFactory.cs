using HospitalityOS.Contracts.Events;
using HospitalityOS.Eventing.Contracts;
using HospitalityOS.Ports.Bindings;
using HospitalityOS.Ports.Ids;
using HospitalityOS.Ports.Persistence;

namespace HospitalityOS.Eventing.Envelopes;

/// <summary>
/// Wraps a committed domain event in the canonical envelope: maps it to its published payload, gives it a new
/// GUID v7 event ID, and stamps when it was recorded. Singleton.
/// </summary>
internal sealed class EventEnvelopeFactory(EventTypeRegistry registry, IBindingResolver<IIdGenerator> idGenerators, TimeProvider timeProvider)
{
    private static readonly IReadOnlyDictionary<string, string> noMetadata = new Dictionary<string, string>();

    public EventEnvelope Create(CommittedEvent committed, EventOrigin origin)
    {
        ArgumentNullException.ThrowIfNull(committed);
        ArgumentNullException.ThrowIfNull(origin);

        MappedEvent mapped = EventContractMapper.Map(committed.Event);
        EventTypeName name = registry.NameOf(mapped.Payload.GetType());

        return new EventEnvelope(
            idGenerators.Resolve().NewId(),
            name.EventType,
            name.Version,
            committed.TenantId,
            mapped.OrganizationId,
            committed.PropertyId,
            committed.AggregateType,
            committed.AggregateId,
            committed.AggregateVersion,
            committed.Event.OccurredAt,
            timeProvider.GetUtcNow(),
            origin.CorrelationId,
            origin.CausationId,
            origin.ActorType,
            origin.ActorId,
            mapped.Payload,
            noMetadata);
    }
}
