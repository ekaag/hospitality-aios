using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Contracts.Events.Guests;

/// <summary>
/// Version 1 of <c>GuestNotificationSent</c>: the messaging provider accepted a message to a guest for delivery.
/// </summary>
/// <param name="GuestNotificationId">The notification.</param>
/// <param name="ConversationId">The guest's conversation.</param>
/// <param name="ProviderName">The messaging provider, e.g. MockMessaging.</param>
/// <param name="ProviderMessageId">The provider's ID for the message.</param>
public sealed record GuestNotificationSentV1(
    Guid GuestNotificationId,
    ConversationId ConversationId,
    string ProviderName,
    string ProviderMessageId) : IEventPayload;
