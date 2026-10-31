using HospitalityOS.Capabilities.Definitions;
using HospitalityOS.Capabilities.Reading;
using HospitalityOS.Contracts;
using HospitalityOS.Contracts.Capabilities;
using HospitalityOS.Contracts.Capabilities.Messaging;
using HospitalityOS.Domain.Guests.Conversations;
using HospitalityOS.Domain.Guests.Notifications;
using HospitalityOS.IntegrationContracts.Messaging;
using HospitalityOS.IntegrationRuntime.Resolution;
using HospitalityOS.Ports.Bindings;
using HospitalityOS.Ports.Ids;
using HospitalityOS.SharedKernel.Results;

namespace HospitalityOS.Capabilities.Messaging;

/// <summary>
/// <c>Messaging.NotifyGuest</c> (spec §11, Epic 11): tells a guest something over their conversation's channel through the
/// property's messaging connector (spec §10), recording a <c>GuestNotification</c> that raises
/// <c>GuestNotificationRequested</c> and then <c>GuestNotificationSent</c>, or <c>GuestNotificationFailed</c> when the
/// provider refuses it or none is configured. A key is one logical message: asking again with a key already used returns
/// that notification, and the connector itself sends a key at most once, so a guest is told once (Journey 19, P8). A
/// provider outage is thrown, saving nothing, so the caller can retry with the same key.
/// </summary>
internal sealed class NotifyGuestCapability(
    IConversationRepository conversations,
    IGuestNotificationRepository notifications,
    IIntegrationResolver<IMessagingConnector> messaging,
    IBindingResolver<IIdGenerator> idGenerators,
    TimeProvider timeProvider) : ICapability<NotifyGuestInput, NotifyGuestOutput>
{
    public static CapabilityDescriptor Descriptor { get; } = new()
    {
        Name = "Messaging.NotifyGuest",
        Version = 1,
        Description = "Sends a guest a message over their conversation's channel, e.g. that their request is done. "
            + "A key names one logical message, which is sent at most once.",
        Authorization = CapabilityAuthorization.Allow(CapabilityCaller.Agent, CapabilityCaller.Manager, CapabilityCaller.Admin),
        ConfirmationPolicy = ConfirmationPolicy.NotRequired,
        SideEffects = SideEffect.External,
        Idempotency = IdempotencyPolicy.KeyRequired,
        AuditClassification = AuditClassification.Sensitive,
    };

    public async Task<Result<NotifyGuestOutput>> ExecuteAsync(NotifyGuestInput input, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);

        if (!string.IsNullOrWhiteSpace(input.Key)
            && await notifications.FindByKeyAsync(input.Key, cancellationToken).ConfigureAwait(false) is { } existing)
        {
            return Output(existing);
        }

        Conversation? conversation = await conversations.GetByIdAsync(input.ConversationId, cancellationToken).ConfigureAwait(false);

        if (conversation is null)
        {
            return Error.Validation("conversationId", "notification.conversation_not_found", "There is no such conversation.");
        }

        Result<GuestNotification> requested = GuestNotification.Request(
            new GuestNotificationId(idGenerators.Resolve().NewId()), conversation, input.Key, input.Body, input.SubjectType, input.SubjectId, timeProvider.GetUtcNow());

        if (requested.IsFailure)
        {
            return requested.Error;
        }

        GuestNotification notification = requested.Value;
        await notifications.AddAsync(notification, cancellationToken).ConfigureAwait(false);
        Result<ResolvedConnector<IMessagingConnector>> resolved = await messaging.ResolveAsync(conversation.PropertyId, cancellationToken).ConfigureAwait(false);

        if (resolved.IsFailure)
        {
            notification.MarkFailed(resolved.Error.Code, timeProvider.GetUtcNow());

            return Output(notification);
        }

        // The web channel's address is the conversation itself.
        OutboundMessage message = new(
            notification.Key,
            new MessageRecipient(EnumMirror.To<MessageChannel>(conversation.Channel), conversation.Id.ToString()),
            notification.Body);
        Result<MessageReceipt> sent = await resolved.Value.Connector.SendAsync(message, cancellationToken).ConfigureAwait(false);

        if (sent.IsFailure)
        {
            notification.MarkFailed(sent.Error.Code, timeProvider.GetUtcNow());
        }
        else
        {
            notification.MarkSent(resolved.Value.ProviderName, sent.Value.MessageId.Value, sent.Value.AcceptedAt);
        }

        return Output(notification);
    }

    private static NotifyGuestOutput Output(GuestNotification notification) =>
        new(Sources.Of(notification), EnumMirror.To<NotificationStatus>(notification.Status), notification.ProviderName, notification.FailureReason);
}
