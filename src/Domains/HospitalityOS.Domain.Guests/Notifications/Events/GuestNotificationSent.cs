using HospitalityOS.SharedKernel.Domain;
using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Domain.Guests.Notifications.Events;

/// <summary>
/// The messaging provider accepted the message for delivery (spec §8).
/// </summary>
public sealed record GuestNotificationSent(
    GuestNotificationId GuestNotificationId,
    ConversationId ConversationId,
    string ProviderName,
    string ProviderMessageId,
    DateTimeOffset OccurredAt) : IDomainEvent;
