using System.Text.Json;
using HospitalityOS.Contracts.Events;
using HospitalityOS.Contracts.Events.Guests;
using HospitalityOS.Contracts.Events.Services;
using HospitalityOS.Contracts.Serialization;
using HospitalityOS.Domain.Guests.Conversations;
using HospitalityOS.Domain.Services.ServiceRequests;
using HospitalityOS.Eventing.Handling;
using HospitalityOS.Ports.Bindings;
using HospitalityOS.Ports.Realtime;
using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Workflows.GuestSurface;

/// <summary>
/// Tells the guest surface that something of a stay changed (J3, the plan's <c>IRealtimeNotifier</c>): a message added to
/// one of its conversations (the guest's own, the concierge's answer, or a notification) or one of its requests created,
/// moved on, or completed is pushed to the stay's channel as <see cref="MessageType"/>, naming what changed, never its text
/// (P10), so the client reads it again through the API. Conversations and requests not linked to a stay are heard by
/// nobody. A push is best effort: a client that misses one still reads everything when it next loads.
/// </summary>
internal sealed class StayChangeNotifier(
    IBindingResolver<IRealtimeNotifier> notifiers,
    IConversationRepository conversations,
    IServiceRequestRepository requests)
    : IEventHandler<GuestMessageReceivedV1>,
        IEventHandler<GuestMessageSentV1>,
        IEventHandler<ServiceRequestCreatedV1>,
        IEventHandler<ServiceRequestUpdatedV1>,
        IEventHandler<ServiceRequestCompletedV1>
{
    public const string OnMessageReceived = "guestsurface.on_message_received";
    public const string OnMessageSent = "guestsurface.on_message_sent";
    public const string OnRequestCreated = "guestsurface.on_request_created";
    public const string OnRequestUpdated = "guestsurface.on_request_updated";
    public const string OnRequestCompleted = "guestsurface.on_request_completed";

    /// <summary>
    /// The message's type, which clients subscribe to.
    /// </summary>
    public const string MessageType = "StayChanged";

    public Task HandleAsync(EventEnvelope envelope, GuestMessageReceivedV1 payload, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(payload);

        return PublishAsync(envelope, payload.StayContextId, new StayChanged(envelope.EventType, payload.ConversationId.Value, null), cancellationToken);
    }

    public async Task HandleAsync(EventEnvelope envelope, GuestMessageSentV1 payload, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(payload);

        Conversation? conversation = await conversations.GetByIdAsync(payload.ConversationId, cancellationToken).ConfigureAwait(false);
        await PublishAsync(envelope, conversation?.StayContextId, new StayChanged(envelope.EventType, payload.ConversationId.Value, null), cancellationToken).ConfigureAwait(false);
    }

    public Task HandleAsync(EventEnvelope envelope, ServiceRequestCreatedV1 payload, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(payload);

        return PublishAsync(envelope, payload.Origin.StayContextId, new StayChanged(envelope.EventType, null, payload.ServiceRequestId.Value), cancellationToken);
    }

    public async Task HandleAsync(EventEnvelope envelope, ServiceRequestUpdatedV1 payload, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(payload);

        ServiceRequest? request = await requests.GetByIdAsync(payload.ServiceRequestId, cancellationToken).ConfigureAwait(false);
        await PublishAsync(envelope, request?.Origin.StayContextId, new StayChanged(envelope.EventType, null, payload.ServiceRequestId.Value), cancellationToken).ConfigureAwait(false);
    }

    public Task HandleAsync(EventEnvelope envelope, ServiceRequestCompletedV1 payload, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(payload);

        return PublishAsync(envelope, payload.Origin.StayContextId, new StayChanged(envelope.EventType, null, payload.ServiceRequestId.Value), cancellationToken);
    }

    private Task PublishAsync(EventEnvelope envelope, StayContextId? stay, StayChanged changed, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(envelope);

        return stay is { } heard
            ? notifiers.Resolve().PublishAsync(
                new RealtimeMessage(GuestChannel.For(envelope.TenantId, heard), MessageType, JsonSerializer.SerializeToElement(changed, ContractJson.Options)),
                cancellationToken)
            : Task.CompletedTask;
    }

    /// <summary>
    /// What a <see cref="MessageType"/> message carries.
    /// </summary>
    /// <param name="Change">The event, e.g. <c>GuestMessageSent</c>.</param>
    /// <param name="ConversationId">The conversation a message was added to.</param>
    /// <param name="ServiceRequestId">The request that changed.</param>
    private sealed record StayChanged(string Change, Guid? ConversationId, Guid? ServiceRequestId);
}
