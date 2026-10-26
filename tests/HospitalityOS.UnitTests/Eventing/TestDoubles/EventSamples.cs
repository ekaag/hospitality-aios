using HospitalityOS.Contracts.Events;
using HospitalityOS.Eventing.Contracts;
using HospitalityOS.Eventing.Envelopes;
using HospitalityOS.Ports.Persistence;
using HospitalityOS.SharedKernel.Domain;
using HospitalityOS.SharedKernel.Execution;
using HospitalityOS.SharedKernel.Ids;
using HospitalityOS.SharedKernel.Tenancy;
using Microsoft.Extensions.Time.Testing;

namespace HospitalityOS.UnitTests.Eventing.TestDoubles;

/// <summary>
/// Envelopes for the <see cref="DomainEventSamples"/>, built by the real factory with fixed IDs and clock.
/// </summary>
internal static class EventSamples
{
    public static TenantId TenantId { get; } = new(DomainEventSamples.Id(100));

    public static PropertyId PropertyId { get; } = new(DomainEventSamples.Id(101));

    public static Guid EventId { get; } = DomainEventSamples.Id(102);

    public const string AggregateType = "SampleAggregate";

    public static Guid AggregateId { get; } = DomainEventSamples.Id(103);

    public static DateTimeOffset RecordedAt { get; } = DomainEventSamples.Now.AddSeconds(1);

    public static EventOrigin Origin { get; } = new(DomainEventSamples.Id(104), DomainEventSamples.Id(105), ActorType.Staff, "maria");

    public static EventEnvelopeFactory Factory() => new(new EventTypeRegistry(), new FixedIdGenerator(EventId), new FakeTimeProvider(RecordedAt));

    public static CommittedEvent Committed(IDomainEvent domainEvent) =>
        new(TenantId, PropertyId, AggregateType, AggregateId, 3, domainEvent);

    public static EventEnvelope Envelope(IDomainEvent domainEvent) => Factory().Create(Committed(domainEvent), Origin);
}
