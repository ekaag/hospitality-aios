using System.Text.Json;
using System.Text.Json.Nodes;
using HospitalityOS.Contracts.Events;
using HospitalityOS.Eventing.Contracts;
using HospitalityOS.Eventing.Serialization;
using HospitalityOS.UnitTests.Eventing.TestDoubles;
using Xunit;

namespace HospitalityOS.UnitTests.Eventing.Serialization;

public sealed class EventSerializerTests
{
    private readonly EventSerializer serializer = new(new EventTypeRegistry());

    [Theory]
    [MemberData(nameof(DomainEventSamples.Names), MemberType = typeof(DomainEventSamples))]
    public void Deserialize_SerializedEnvelope_RoundTripsEveryEvent(string domainEvent)
    {
        EventEnvelope envelope = EventSamples.Envelope(DomainEventSamples.Get(domainEvent));

        EventEnvelope read = serializer.Deserialize(serializer.Serialize(envelope));

        Assert.Equal(envelope with { Metadata = read.Metadata }, read);
        Assert.Equal(envelope.Metadata, read.Metadata);
    }

    [Fact]
    public void Serialize_Envelope_WritesSpecFieldsInCamelCaseWithPayloadLast()
    {
        EventEnvelope envelope = EventSamples.Envelope(DomainEventSamples.Get("HumanActionCompleted"));

        JsonObject json = JsonNode.Parse(serializer.Serialize(envelope))!.AsObject();

        Assert.Equal(
            ["eventId", "eventType", "eventVersion", "tenantId", "organizationId", "propertyId", "aggregateType", "aggregateId", "aggregateVersion",
             "occurredAt", "recordedAt", "correlationId", "causationId", "actorType", "actorId", "payload", "metadata"],
            json.Select(property => property.Key));
        Assert.Equal("HumanActionCompleted", (string?)json["eventType"]);
        Assert.Equal(1, (int?)json["eventVersion"]);
        Assert.Equal("Staff", (string?)json["actorType"]);
        Assert.Equal(EventSamples.PropertyId.Value.ToString(), (string?)json["propertyId"]);
        Assert.Equal(DomainEventSamples.Id(13).ToString(), (string?)json["payload"]!["staffMemberId"]);
    }

    [Fact]
    public void Deserialize_UnknownEventType_Throws()
    {
        JsonObject json = SerializedSample();
        json["eventType"] = "NoSuchEvent";

        JsonException exception = Assert.Throws<JsonException>(() => serializer.Deserialize(json.ToJsonString()));

        Assert.Contains("Unknown event type NoSuchEvent v1", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Deserialize_NewerVersionThanThisBuildKnows_Throws()
    {
        JsonObject json = SerializedSample();
        json["eventVersion"] = 2;

        Assert.Throws<JsonException>(() => serializer.Deserialize(json.ToJsonString()));
    }

    [Theory]
    [InlineData("tenantId")]
    [InlineData("correlationId")]
    [InlineData("payload")]
    public void Deserialize_MissingRequiredField_Throws(string field)
    {
        JsonObject json = SerializedSample();
        json.Remove(field);

        Assert.Throws<JsonException>(() => serializer.Deserialize(json.ToJsonString()));
    }

    [Fact]
    public void Deserialize_PayloadMissingRequiredField_Throws()
    {
        JsonObject json = SerializedSample();
        json["payload"]!.AsObject().Remove("staffMemberId");

        Assert.Throws<JsonException>(() => serializer.Deserialize(json.ToJsonString()));
    }

    [Fact]
    public void Serialize_EnvelopeTypeDisagreesWithPayload_Throws()
    {
        EventEnvelope envelope = EventSamples.Envelope(DomainEventSamples.Get("HumanActionCompleted")) with { EventType = "HumanActionFailed" };

        Assert.Throws<InvalidOperationException>(() => serializer.Serialize(envelope));
    }

    private JsonObject SerializedSample() =>
        JsonNode.Parse(serializer.Serialize(EventSamples.Envelope(DomainEventSamples.Get("HumanActionCompleted"))))!.AsObject();
}
