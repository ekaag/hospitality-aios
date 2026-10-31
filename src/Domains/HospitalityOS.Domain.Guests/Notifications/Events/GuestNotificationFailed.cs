using HospitalityOS.SharedKernel.Domain;
using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Domain.Guests.Notifications.Events;

/// <summary>
/// The message could not be sent; the guest was not told.
/// </summary>
public sealed record GuestNotificationFailed(
    GuestNotificationId GuestNotificationId,
    ConversationId ConversationId,
    string Reason,
    DateTimeOffset OccurredAt) : IDomainEvent;
