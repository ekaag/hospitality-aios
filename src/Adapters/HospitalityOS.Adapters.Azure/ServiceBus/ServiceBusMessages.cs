using System.Globalization;
using Azure.Messaging.ServiceBus;
using HospitalityOS.Ports.Eventing;
using HospitalityOS.Ports.Scheduling;
using HospitalityOS.SharedKernel.Tenancy;

namespace HospitalityOS.Adapters.Azure.ServiceBus;

/// <summary>
/// Event and scheduled messages as Service Bus messages: the envelope or payload is the body; the fields carried
/// beside it are the message ID, correlation ID, subject, and application properties. Times travel as round-trip
/// text, since AMQP timestamps keep only milliseconds.
/// </summary>
internal static class ServiceBusMessages
{
    private const string Json = "application/json";
    private const string EventVersion = "eventVersion";
    private const string Tenant = "tenantId";
    private const string AggregateType = "aggregateType";
    private const string AggregateId = "aggregateId";
    private const string RecordedAt = "recordedAt";
    private const string Causation = "causationId";
    private const string DueAt = "dueAt";

    public static ServiceBusMessage ToServiceBus(EventMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);

        ServiceBusMessage sent = new(BinaryData.FromString(message.Envelope))
        {
            MessageId = message.EventId.ToString("D"),
            CorrelationId = message.CorrelationId.ToString("D"),
            Subject = message.EventType,
            ContentType = Json,
        };
        sent.ApplicationProperties[EventVersion] = message.EventVersion;
        sent.ApplicationProperties[Tenant] = message.TenantId.Value.ToString("D");
        sent.ApplicationProperties[AggregateType] = message.AggregateType;
        sent.ApplicationProperties[AggregateId] = message.AggregateId.ToString("D");
        sent.ApplicationProperties[RecordedAt] = message.RecordedAt.ToString("O", CultureInfo.InvariantCulture);

        return sent;
    }

    /// <exception cref="FormatException">The message was not written by <see cref="ToServiceBus(EventMessage)"/>.</exception>
    public static EventMessage ToEvent(ServiceBusReceivedMessage received)
    {
        ArgumentNullException.ThrowIfNull(received);

        return new EventMessage(
            ParseGuid(received.MessageId, "message ID"),
            received.Subject is { Length: > 0 } type ? type : throw new FormatException("The message has no subject (event type)."),
            received.ApplicationProperties.TryGetValue(EventVersion, out object? version) && version is int number
                ? number
                : throw new FormatException($"The message has no integer {EventVersion}."),
            new TenantId(ParseGuid(Text(received, Tenant), Tenant)),
            Text(received, AggregateType),
            ParseGuid(Text(received, AggregateId), AggregateId),
            ParseGuid(received.CorrelationId, "correlation ID"),
            ParseTime(Text(received, RecordedAt), RecordedAt),
            received.Body.ToString());
    }

    public static ServiceBusMessage ToServiceBus(ScheduledMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);

        ServiceBusMessage sent = new(BinaryData.FromString(message.Payload))
        {
            MessageId = message.MessageId.ToString("D"),
            CorrelationId = message.CorrelationId.ToString("D"),
            Subject = message.Handler,
            ContentType = Json,
        };
        sent.ApplicationProperties[Tenant] = message.TenantId.Value.ToString("D");
        sent.ApplicationProperties[DueAt] = message.DueAt.ToString("O", CultureInfo.InvariantCulture);

        if (message.CausationId is { } causation)
        {
            sent.ApplicationProperties[Causation] = causation.ToString("D");
        }

        return sent;
    }

    /// <exception cref="FormatException">The message was not written by <see cref="ToServiceBus(ScheduledMessage)"/>.</exception>
    public static ScheduledMessage ToScheduled(ServiceBusReceivedMessage received)
    {
        ArgumentNullException.ThrowIfNull(received);

        return new ScheduledMessage(
            ParseGuid(received.MessageId, "message ID"),
            received.Subject is { Length: > 0 } handler ? handler : throw new FormatException("The message has no subject (handler)."),
            new TenantId(ParseGuid(Text(received, Tenant), Tenant)),
            ParseGuid(received.CorrelationId, "correlation ID"),
            received.ApplicationProperties.ContainsKey(Causation) ? ParseGuid(Text(received, Causation), Causation) : null,
            received.Body.ToString(),
            ParseTime(Text(received, DueAt), DueAt));
    }

    private static string Text(ServiceBusReceivedMessage received, string property) =>
        received.ApplicationProperties.TryGetValue(property, out object? value) && value is string { Length: > 0 } text
            ? text
            : throw new FormatException($"The message has no {property}.");

    private static Guid ParseGuid(string? value, string what) =>
        Guid.TryParseExact(value, "D", out Guid parsed) ? parsed : throw new FormatException($"The message's {what} '{value}' is not a GUID.");

    private static DateTimeOffset ParseTime(string value, string what) =>
        DateTimeOffset.TryParseExact(value, "O", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTimeOffset parsed)
            ? parsed
            : throw new FormatException($"The message's {what} '{value}' is not a round-trip time.");
}
