using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Contracts.Api.Conversations;

/// <summary>
/// One message in a conversation.
/// </summary>
/// <param name="Id">Platform-owned ID.</param>
/// <param name="ConversationId">The conversation.</param>
/// <param name="Sender">Who wrote it.</param>
/// <param name="Text">The text, trimmed.</param>
/// <param name="ClientMessageId">The client's own ID for a guest's message, if it sent one.</param>
/// <param name="SentAt">When it was recorded (UTC).</param>
public sealed record GuestMessageResponse(
    GuestMessageId Id,
    ConversationId ConversationId,
    MessageSender Sender,
    string Text,
    string? ClientMessageId,
    DateTimeOffset SentAt);
