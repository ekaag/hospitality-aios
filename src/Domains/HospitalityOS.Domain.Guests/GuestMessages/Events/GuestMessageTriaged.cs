using HospitalityOS.SharedKernel.Domain;
using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Domain.Guests.GuestMessages.Events;

/// <summary>
/// A guest's message was triaged: the controller for its intent takes it from here (P1).
/// </summary>
public sealed record GuestMessageTriaged(
    GuestMessageId GuestMessageId,
    ConversationId ConversationId,
    PropertyId PropertyId,
    GuestId? GuestId,
    StayContextId? StayContextId,
    MessageIntent Intent,
    DateTimeOffset OccurredAt) : IDomainEvent;
