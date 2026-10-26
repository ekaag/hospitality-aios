using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Contracts.Events.Guests;

/// <summary>
/// Version 1 of <c>ConversationUpdated</c>: a conversation was linked to a stay or closed.
/// </summary>
/// <param name="ConversationId">The conversation.</param>
/// <param name="GuestId">The guest.</param>
/// <param name="StayContextId">The stay.</param>
/// <param name="Status">The status after the change (domain enum name).</param>
public sealed record ConversationUpdatedV1(
    ConversationId ConversationId,
    GuestId? GuestId,
    StayContextId? StayContextId,
    string Status) : IEventPayload;
