using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Contracts.Events.Values;

/// <summary>
/// Who or what a service request came from.
/// </summary>
/// <param name="GuestId">The guest, if guest-initiated.</param>
/// <param name="StayContextId">The stay, if known.</param>
/// <param name="ConversationId">The conversation, if it came from one.</param>
/// <param name="SourceMessageId">The message it was created from, if any.</param>
public sealed record RequestOriginData(
    GuestId? GuestId,
    StayContextId? StayContextId,
    ConversationId? ConversationId,
    GuestMessageId? SourceMessageId);
