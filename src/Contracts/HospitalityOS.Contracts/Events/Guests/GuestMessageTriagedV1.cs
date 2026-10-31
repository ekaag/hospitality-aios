using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Contracts.Events.Guests;

/// <summary>
/// Version 1 of <c>GuestMessageTriaged</c>: a guest's message was triaged, and the controller for its intent takes it from
/// here. Carries IDs only, never the text (P10).
/// </summary>
/// <param name="GuestMessageId">The message.</param>
/// <param name="ConversationId">The conversation.</param>
/// <param name="PropertyId">The property.</param>
/// <param name="GuestId">The guest, if identified.</param>
/// <param name="StayContextId">The stay, if identified.</param>
/// <param name="Intent">What it asks for (domain enum name): <c>Question</c>, <c>ServiceRequest</c>, or <c>RequestStatus</c>.</param>
public sealed record GuestMessageTriagedV1(
    GuestMessageId GuestMessageId,
    ConversationId ConversationId,
    PropertyId PropertyId,
    GuestId? GuestId,
    StayContextId? StayContextId,
    string Intent) : IEventPayload;
