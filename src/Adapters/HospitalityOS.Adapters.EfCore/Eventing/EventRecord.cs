using HospitalityOS.Ports.Eventing;
using HospitalityOS.SharedKernel.Tenancy;

namespace HospitalityOS.Adapters.EfCore.Eventing;

/// <summary>
/// A row of the append-only event log. <see cref="Position"/> is assigned by the database.
/// </summary>
internal sealed class EventRecord
{
    public long Position { get; set; }

    public Guid EventId { get; set; }

    public string EventType { get; set; } = string.Empty;

    public int EventVersion { get; set; }

    public TenantId TenantId { get; set; }

    public string AggregateType { get; set; } = string.Empty;

    public Guid AggregateId { get; set; }

    public Guid CorrelationId { get; set; }

    public DateTimeOffset RecordedAt { get; set; }

    public string Envelope { get; set; } = string.Empty;

    public static EventRecord From(EventMessage message) => new()
    {
        EventId = message.EventId,
        EventType = message.EventType,
        EventVersion = message.EventVersion,
        TenantId = message.TenantId,
        AggregateType = message.AggregateType,
        AggregateId = message.AggregateId,
        CorrelationId = message.CorrelationId,
        RecordedAt = message.RecordedAt,
        Envelope = message.Envelope,
    };

    public EventMessage ToMessage() =>
        new(EventId, EventType, EventVersion, TenantId, AggregateType, AggregateId, CorrelationId, RecordedAt, Envelope);
}
