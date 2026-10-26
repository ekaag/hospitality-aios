using HospitalityOS.Ports.Eventing;
using HospitalityOS.SharedKernel.Tenancy;

namespace HospitalityOS.Adapters.EfCore.Eventing;

/// <summary>
/// A queued event and its publish state; deleted once published. <see cref="Sequence"/> is assigned by the database
/// and orders the queue.
/// </summary>
internal sealed class OutboxRecord
{
    public long Sequence { get; set; }

    public Guid EventId { get; set; }

    public string EventType { get; set; } = string.Empty;

    public int EventVersion { get; set; }

    public TenantId TenantId { get; set; }

    public string AggregateType { get; set; } = string.Empty;

    public Guid AggregateId { get; set; }

    public Guid CorrelationId { get; set; }

    public DateTimeOffset RecordedAt { get; set; }

    public string Envelope { get; set; } = string.Empty;

    public int Attempts { get; set; }

    public string? LastError { get; set; }

    public DateTimeOffset? NextAttemptAt { get; set; }

    public DateTimeOffset? LeaseUntil { get; set; }

    public DateTimeOffset? DeadLetteredAt { get; set; }

    public static OutboxRecord From(EventMessage message) => new()
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

    public OutboxEntry ToEntry() =>
        new(new EventMessage(EventId, EventType, EventVersion, TenantId, AggregateType, AggregateId, CorrelationId, RecordedAt, Envelope), Attempts, LastError);
}
