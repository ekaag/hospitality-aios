using HospitalityOS.SharedKernel.Domain;
using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Domain.Guests.GuestMessages.Events;

/// <summary>
/// A guest sent a message (spec §8); raised before any processing. Handlers load the message for its text,
/// so guest-written content is not copied into the event log.
/// </summary>
public sealed record GuestMessageReceived(
    GuestMessageId GuestMessageId,
    ConversationId ConversationId,
    PropertyId PropertyId,
    GuestId? GuestId,
    StayContextId? StayContextId,
    DateTimeOffset OccurredAt) : IDomainEvent;
