using HospitalityOS.Domain.Guests.Conversations;
using HospitalityOS.SharedKernel.Domain;
using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Domain.Guests.Notifications.Events;

/// <summary>
/// A message to a guest was requested (spec §8), e.g. that their towels were delivered.
/// </summary>
public sealed record GuestNotificationRequested(
    GuestNotificationId GuestNotificationId,
    PropertyId PropertyId,
    ConversationId ConversationId,
    GuestId? GuestId,
    ConversationChannel Channel,
    string? SubjectType,
    Guid? SubjectId,
    DateTimeOffset OccurredAt) : IDomainEvent;
