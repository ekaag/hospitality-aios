using HospitalityOS.Contracts.Events;
using HospitalityOS.Eventing.Contracts;
using HospitalityOS.Eventing.Envelopes;
using HospitalityOS.Eventing.Serialization;
using HospitalityOS.Ports.Bindings;
using HospitalityOS.Ports.Eventing;
using HospitalityOS.Ports.Ids;
using HospitalityOS.UnitTests.Eventing.TestDoubles;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace HospitalityOS.UnitTests.Eventing.Envelopes;

public sealed class EventMessageFactoryTests
{
    private readonly SequentialIds ids = new();
    private readonly EventSerializer serializer = new(new EventTypeRegistry());

    [Fact]
    public void Create_CommittedEvent_CopiesRoutingFieldsFromTheSerializedEnvelope()
    {
        EventMessageFactory factory = Factory(new ScopedEventOrigin(ids));

        EventMessage message = factory.Create(EventSamples.Committed(DomainEventSamples.Get("HumanActionCompleted")));

        EventEnvelope envelope = serializer.Deserialize(message.Envelope);
        Assert.Equal(
            new EventMessage(envelope.EventId, envelope.EventType, envelope.EventVersion, envelope.TenantId, envelope.AggregateType, envelope.AggregateId, envelope.CorrelationId, envelope.RecordedAt, message.Envelope),
            message);
        Assert.Equal(("HumanActionCompleted", ActorType.System, (Guid?)null), (envelope.EventType, envelope.ActorType, envelope.CausationId));
    }

    [Fact]
    public void Create_EventsInOneScope_ShareACorrelationThatOtherScopesDoNot()
    {
        EventMessageFactory firstScope = Factory(new ScopedEventOrigin(ids));
        EventMessageFactory secondScope = Factory(new ScopedEventOrigin(ids));

        EventMessage first = firstScope.Create(EventSamples.Committed(DomainEventSamples.Get("ServiceRequestCreated")));
        EventMessage second = firstScope.Create(EventSamples.Committed(DomainEventSamples.Get("HumanActionRequested")));
        EventMessage other = secondScope.Create(EventSamples.Committed(DomainEventSamples.Get("GuestRegistered")));

        Assert.Equal(first.CorrelationId, second.CorrelationId);
        Assert.NotEqual(first.CorrelationId, other.CorrelationId);
        Assert.NotEqual(first.EventId, second.EventId);
    }

    private EventMessageFactory Factory(ScopedEventOrigin origin) =>
        new(new EventEnvelopeFactory(new EventTypeRegistry(), ids, new FakeTimeProvider(EventSamples.RecordedAt)), serializer, origin);

    private sealed class SequentialIds : IIdGenerator, IBindingResolver<IIdGenerator>
    {
        private int next;

        public Guid NewId() => DomainEventSamples.Id(500 + Interlocked.Increment(ref next));

        public IIdGenerator Resolve(BindingContext context) => this;
    }
}
