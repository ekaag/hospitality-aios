using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Contracts.Events.Guests;

/// <summary>
/// Version 1 of <c>GuestMessageReceived</c>: a guest sent a message; the concierge starts here. Carries IDs only, never the text (P10).
/// </summary>
/// <param name="GuestMessageId">The message.</param>
/// <param name="ConversationId">The conversation.</param>
/// <param name="PropertyId">The property.</param>
/// <param name="GuestId">The guest.</param>
/// <param name="StayContextId">The stay.</param>
public sealed record GuestMessageReceivedV1(
    GuestMessageId GuestMessageId,
    ConversationId ConversationId,
    PropertyId PropertyId,
    GuestId? GuestId,
    StayContextId? StayContextId) : IEventPayload;
