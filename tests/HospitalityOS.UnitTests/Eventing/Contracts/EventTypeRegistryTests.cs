using HospitalityOS.Contracts.Events;
using HospitalityOS.Contracts.Events.Operations;
using HospitalityOS.Eventing.Contracts;
using HospitalityOS.UnitTests.Eventing.TestDoubles;
using Xunit;

namespace HospitalityOS.UnitTests.Eventing.Contracts;

public sealed class EventTypeRegistryTests
{
    [Fact]
    public void All_ListsOnePublishedVersionPerDomainEvent()
    {
        EventTypeRegistry registry = new();

        Assert.Equal(DomainEventSamples.All.Count, registry.All.Count);
    }

    [Fact]
    public void NameOf_PayloadType_ReadsEventTypeAndVersionFromTypeName()
    {
        EventTypeName name = new EventTypeRegistry().NameOf(typeof(HumanActionCompletedV1));

        Assert.Equal(new EventTypeName("HumanActionCompleted", 1), name);
    }

    [Fact]
    public void PayloadTypeFor_KnownAndUnknownVersions_ReturnsTypeOrNull()
    {
        EventTypeRegistry registry = new();

        Assert.Equal(typeof(HumanActionCompletedV1), registry.PayloadTypeFor("HumanActionCompleted", 1));
        Assert.Null(registry.PayloadTypeFor("HumanActionCompleted", 2));
        Assert.Null(registry.PayloadTypeFor("NoSuchEvent", 1));
    }

    [Fact]
    public void Constructor_PayloadWithoutVersionSuffix_Throws()
    {
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => new EventTypeRegistry([typeof(ThingHappened)]));

        Assert.Contains("must be named <EventType>V<version>", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Constructor_TwoPayloadsForSameTypeAndVersion_Throws()
    {
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => new EventTypeRegistry([typeof(First.ThingHappenedV1), typeof(Second.ThingHappenedV1)]));

        Assert.Contains("both publish ThingHappened v1", exception.Message, StringComparison.Ordinal);
    }

    private sealed record ThingHappened : IEventPayload;

    private static class First
    {
        public sealed record ThingHappenedV1 : IEventPayload;
    }

    private static class Second
    {
        public sealed record ThingHappenedV1 : IEventPayload;
    }
}
