using HospitalityOS.Contracts.Events;
using HospitalityOS.Contracts.Events.Operations;
using HospitalityOS.Domain.Operations.HumanActions.Events;
using HospitalityOS.SharedKernel.Execution;
using HospitalityOS.SharedKernel.Ids;
using HospitalityOS.UnitTests.Eventing.TestDoubles;
using Xunit;

namespace HospitalityOS.UnitTests.Eventing.Envelopes;

public sealed class EventEnvelopeFactoryTests
{
    [Fact]
    public void Create_CommittedEvent_FillsEveryEnvelopeField()
    {
        HumanActionCompleted completed = (HumanActionCompleted)DomainEventSamples.Get("HumanActionCompleted");

        EventEnvelope envelope = EventSamples.Factory().Create(EventSamples.Committed(completed), EventSamples.Origin);

        Assert.Equal(EventSamples.EventId, envelope.EventId);
        Assert.Equal("HumanActionCompleted", envelope.EventType);
        Assert.Equal(1, envelope.EventVersion);
        Assert.Equal(EventSamples.TenantId, envelope.TenantId);
        Assert.Null(envelope.OrganizationId);
        Assert.Equal(EventSamples.PropertyId, envelope.PropertyId);
        Assert.Equal(EventSamples.AggregateType, envelope.AggregateType);
        Assert.Equal(EventSamples.AggregateId, envelope.AggregateId);
        Assert.Equal(3, envelope.AggregateVersion);
        Assert.Equal(completed.OccurredAt, envelope.OccurredAt);
        Assert.Equal(EventSamples.RecordedAt, envelope.RecordedAt);
        Assert.Equal(EventSamples.Origin.CorrelationId, envelope.CorrelationId);
        Assert.Equal(EventSamples.Origin.CausationId, envelope.CausationId);
        Assert.Equal(ActorType.Staff, envelope.ActorType);
        Assert.Equal("maria", envelope.ActorId);
        Assert.Equal(new HumanActionCompletedV1(completed.HumanActionId.Value, completed.StaffMemberId, completed.ServiceRequestId), envelope.Payload);
        Assert.Empty(envelope.Metadata);
    }

    [Fact]
    public void Create_OrganizationEvent_SetsOrganizationId()
    {
        EventEnvelope envelope = EventSamples.Envelope(DomainEventSamples.Get("OrganizationCreated"));

        Assert.Equal(new OrganizationId(DomainEventSamples.Id(2)), envelope.OrganizationId);
    }
}
