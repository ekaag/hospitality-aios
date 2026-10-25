using HospitalityOS.SharedKernel.Domain;
using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Domain.Guests.GuestMessages.Events;

/// <summary>
/// The concierge, staff, or system posted a message to a guest conversation.
/// </summary>
public sealed record GuestMessageSent(
    GuestMessageId GuestMessageId,
    ConversationId ConversationId,
    PropertyId PropertyId,
    MessageSender Sender,
    DateTimeOffset OccurredAt) : IDomainEvent;
