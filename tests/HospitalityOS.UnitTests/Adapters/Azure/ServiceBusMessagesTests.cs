using Azure.Messaging.ServiceBus;
using HospitalityOS.Adapters.Azure.ServiceBus;
using HospitalityOS.Ports.Eventing;
using HospitalityOS.Ports.Scheduling;
using HospitalityOS.SharedKernel.Tenancy;
using Xunit;

namespace HospitalityOS.UnitTests.Adapters.Azure;

/// <summary>
/// Events and scheduled messages come back from Service Bus exactly as they were sent, to the tick, so the
/// broker contract tests compare whole records.
/// </summary>
public sealed class ServiceBusMessagesTests
{
    private static readonly EventMessage @event = new(
        Guid.Parse("01890a5d-ac96-774b-bcce-b30209900101"),
        "HumanActionCompleted",
        2,
        new TenantId(Guid.Parse("01890a5d-ac96-774b-bcce-b30209900001")),
        "HumanAction",
        Guid.Parse("01890a5d-ac96-774b-bcce-b30209900201"),
        Guid.Parse("01890a5d-ac96-774b-bcce-b30209900301"),
        new DateTimeOffset(2026, 11, 1, 9, 30, 15, TimeSpan.FromHours(1)).AddTicks(1234567),
        """{"eventId":"x","payload":{"room":"412"}}""");

    private static readonly ScheduledMessage scheduled = new(
        Guid.Parse("01890a5d-ac96-774b-bcce-b30209900401"),
        "humanaction.reroute_unaccepted",
        new TenantId(Guid.Parse("01890a5d-ac96-774b-bcce-b30209900001")),
        Guid.Parse("01890a5d-ac96-774b-bcce-b30209900301"),
        Guid.Parse("01890a5d-ac96-774b-bcce-b30209900101"),
        """{"humanActionId":"y"}""",
        new DateTimeOffset(2026, 11, 1, 9, 45, 0, TimeSpan.Zero).AddTicks(7));

    [Fact]
    public void Event_SentAndReceived_IsTheSameEvent()
    {
        ServiceBusMessage sent = ServiceBusMessages.ToServiceBus(@event);

        Assert.Equal(@event, ServiceBusMessages.ToEvent(Received(sent)));
        Assert.Equal(@event.EventType, sent.Subject);
        Assert.Equal(@event.EventId.ToString("D"), sent.MessageId);
    }

    [Fact]
    public void Scheduled_SentAndReceived_IsTheSameMessage()
    {
        Assert.Equal(scheduled, ServiceBusMessages.ToScheduled(Received(ServiceBusMessages.ToServiceBus(scheduled))));
    }

    [Fact]
    public void Scheduled_WithoutCausation_SentAndReceived_HasNoCausation()
    {
        ScheduledMessage uncaused = scheduled with { CausationId = null };

        Assert.Equal(uncaused, ServiceBusMessages.ToScheduled(Received(ServiceBusMessages.ToServiceBus(uncaused))));
    }

    [Theory]
    [InlineData("tenantId")]
    [InlineData("recordedAt")]
    [InlineData("eventVersion")]
    public void Event_MissingAProperty_IsUnreadable(string property)
    {
        ServiceBusMessage sent = ServiceBusMessages.ToServiceBus(@event);
        sent.ApplicationProperties.Remove(property);

        Assert.Throws<FormatException>(() => ServiceBusMessages.ToEvent(Received(sent)));
    }

    private static ServiceBusReceivedMessage Received(ServiceBusMessage sent) =>
        ServiceBusModelFactory.ServiceBusReceivedMessage(
            body: sent.Body,
            messageId: sent.MessageId,
            correlationId: sent.CorrelationId,
            subject: sent.Subject,
            contentType: sent.ContentType,
            properties: new Dictionary<string, object>(sent.ApplicationProperties));
}
