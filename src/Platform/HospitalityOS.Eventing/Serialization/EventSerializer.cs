using System.Text.Json;
using HospitalityOS.Contracts.Events;
using HospitalityOS.Eventing.Contracts;
using HospitalityOS.SharedKernel.Execution;
using HospitalityOS.SharedKernel.Ids;
using HospitalityOS.SharedKernel.Tenancy;

namespace HospitalityOS.Eventing.Serialization;

/// <summary>
/// Writes envelopes as JSON for the outbox, transport, and event store, and reads them back with the payload
/// typed by <c>eventType</c> + <c>eventVersion</c>. Reading an event type this build does not know fails, so
/// the message is retried or dead-lettered rather than half-processed. Singleton.
/// </summary>
internal sealed class EventSerializer(EventTypeRegistry registry)
{
    public string Serialize(EventEnvelope envelope)
    {
        ArgumentNullException.ThrowIfNull(envelope);

        EventTypeName name = registry.NameOf(envelope.Payload.GetType());

        if (name != new EventTypeName(envelope.EventType, envelope.EventVersion))
        {
            throw new InvalidOperationException($"The envelope says {envelope.EventType} v{envelope.EventVersion} but carries a {name} payload.");
        }

        WireEnvelope wire = new(
            envelope.EventId,
            envelope.EventType,
            envelope.EventVersion,
            envelope.TenantId,
            envelope.OrganizationId,
            envelope.PropertyId,
            envelope.AggregateType,
            envelope.AggregateId,
            envelope.AggregateVersion,
            envelope.OccurredAt,
            envelope.RecordedAt,
            envelope.CorrelationId,
            envelope.CausationId,
            envelope.ActorType,
            envelope.ActorId,
            JsonSerializer.SerializeToElement(envelope.Payload, envelope.Payload.GetType(), EventJson.Options),
            envelope.Metadata);

        return JsonSerializer.Serialize(wire, EventJson.Options);
    }

    public EventEnvelope Deserialize(string json)
    {
        ArgumentException.ThrowIfNullOrEmpty(json);

        WireEnvelope wire = JsonSerializer.Deserialize<WireEnvelope>(json, EventJson.Options)
            ?? throw new JsonException("The event is null.");
        Type payloadType = registry.PayloadTypeFor(wire.EventType, wire.EventVersion)
            ?? throw new JsonException($"Unknown event type {wire.EventType} v{wire.EventVersion}; this build cannot read it.");
        IEventPayload payload = (IEventPayload?)wire.Payload.Deserialize(payloadType, EventJson.Options)
            ?? throw new JsonException($"The {wire.EventType} payload is null.");

        return new EventEnvelope(
            wire.EventId,
            wire.EventType,
            wire.EventVersion,
            wire.TenantId,
            wire.OrganizationId,
            wire.PropertyId,
            wire.AggregateType,
            wire.AggregateId,
            wire.AggregateVersion,
            wire.OccurredAt,
            wire.RecordedAt,
            wire.CorrelationId,
            wire.CausationId,
            wire.ActorType,
            wire.ActorId,
            payload,
            wire.Metadata);
    }

    /// <summary>
    /// The envelope as written: the same fields in the same order, with the payload as raw JSON.
    /// </summary>
    private sealed record WireEnvelope(
        Guid EventId,
        string EventType,
        int EventVersion,
        TenantId TenantId,
        OrganizationId? OrganizationId,
        PropertyId? PropertyId,
        string AggregateType,
        Guid AggregateId,
        long AggregateVersion,
        DateTimeOffset OccurredAt,
        DateTimeOffset RecordedAt,
        Guid CorrelationId,
        Guid? CausationId,
        ActorType ActorType,
        string? ActorId,
        JsonElement Payload,
        IReadOnlyDictionary<string, string> Metadata);
}
