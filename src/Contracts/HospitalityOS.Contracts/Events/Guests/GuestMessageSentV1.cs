using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Contracts.Events.Guests;

/// <summary>
/// Version 1 of <c>GuestMessageSent</c>: the concierge, staff, or system sent a message. Carries IDs only, never the text (P10).
/// </summary>
/// <param name="GuestMessageId">The message.</param>
/// <param name="ConversationId">The conversation.</param>
/// <param name="PropertyId">The property.</param>
/// <param name="Sender">Who wrote it (domain enum name).</param>
public sealed record GuestMessageSentV1(
    GuestMessageId GuestMessageId,
    ConversationId ConversationId,
    PropertyId PropertyId,
    string Sender) : IEventPayload;
