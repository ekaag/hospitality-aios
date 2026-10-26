using HospitalityOS.SharedKernel.Tenancy;

namespace HospitalityOS.Ports.Eventing;

/// <summary>
/// One event as stores and transports carry it: the serialized canonical envelope, plus the few envelope fields
/// they need to route, order, and query without reading the JSON. Ports cannot see the envelope type (P9), so it
/// travels as text.
/// </summary>
/// <param name="EventId">Globally unique event ID; the de-duplication key everywhere (P8).</param>
/// <param name="EventType">The event type, e.g. <c>HumanActionCompleted</c>.</param>
/// <param name="EventVersion">The payload schema version.</param>
/// <param name="TenantId">The owning tenant.</param>
/// <param name="AggregateType">The aggregate that raised it.</param>
/// <param name="AggregateId">The aggregate's ID.</param>
/// <param name="CorrelationId">The end-to-end flow it belongs to.</param>
/// <param name="RecordedAt">When it was recorded (UTC).</param>
/// <param name="Envelope">The canonical envelope as JSON; the source of truth for every other field.</param>
public sealed record EventMessage(
    Guid EventId,
    string EventType,
    int EventVersion,
    TenantId TenantId,
    string AggregateType,
    Guid AggregateId,
    Guid CorrelationId,
    DateTimeOffset RecordedAt,
    string Envelope);
