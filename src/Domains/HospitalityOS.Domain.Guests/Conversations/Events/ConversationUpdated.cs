using HospitalityOS.SharedKernel.Domain;
using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Domain.Guests.Conversations.Events;

/// <summary>
/// A conversation was linked to a guest's stay or closed. Carries the full new state, not a diff.
/// </summary>
public sealed record ConversationUpdated(
    ConversationId ConversationId,
    GuestId? GuestId,
    StayContextId? StayContextId,
    ConversationStatus Status,
    DateTimeOffset OccurredAt) : IDomainEvent;
