using HospitalityOS.SharedKernel.Execution;
using HospitalityOS.SharedKernel.Ids;
using HospitalityOS.SharedKernel.Tenancy;

namespace HospitalityOS.Contracts.Events;

/// <summary>
/// The canonical envelope every event travels in (spec §8, P1): identity and schema version, ownership, the
/// aggregate it is about, timing, correlation and causation (P11), the actor, and the versioned payload. Handlers
/// de-duplicate on <see cref="EventId"/> (P8); replay orders by <see cref="RecordedAt"/> and, per aggregate,
/// <see cref="AggregateVersion"/>.
/// </summary>
/// <param name="EventId">Globally unique event ID (GUID v7).</param>
/// <param name="EventType">The event type, e.g. <c>HumanActionCompleted</c>.</param>
/// <param name="EventVersion">The payload schema version, from 1.</param>
/// <param name="TenantId">The owning tenant.</param>
/// <param name="OrganizationId">The organization, when the event identifies it.</param>
/// <param name="PropertyId">The property, or <c>null</c> for tenant-wide aggregates (organizations, guests).</param>
/// <param name="AggregateType">The aggregate that raised the event, e.g. <c>HumanAction</c>.</param>
/// <param name="AggregateId">The aggregate's ID.</param>
/// <param name="AggregateVersion">The aggregate's version immediately after this event; orders one aggregate's events.</param>
/// <param name="OccurredAt">When the change happened (UTC).</param>
/// <param name="RecordedAt">When the event was recorded for publication (UTC).</param>
/// <param name="CorrelationId">Shared by every event, capability call, and inference call of one end-to-end flow.</param>
/// <param name="CausationId">The event or command that directly caused this one, if any.</param>
/// <param name="ActorType">Who caused it.</param>
/// <param name="ActorId">The actor's identifier (staff member, guest, or agent name), if known.</param>
/// <param name="Payload">The versioned body; its type matches <paramref name="EventType"/> and <paramref name="EventVersion"/>.</param>
/// <param name="Metadata">Extra string attributes for transport and tracing; never business data.</param>
public sealed record EventEnvelope(
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
    IEventPayload Payload,
    IReadOnlyDictionary<string, string> Metadata);
