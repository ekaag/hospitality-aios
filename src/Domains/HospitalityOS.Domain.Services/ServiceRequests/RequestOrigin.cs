using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Domain.Services.ServiceRequests;

/// <summary>
/// Who and what a <see cref="ServiceRequest"/> came from. All optional: staff may raise requests directly.
/// </summary>
/// <param name="GuestId">The requesting guest.</param>
/// <param name="StayContextId">The guest's stay, which identifies their room.</param>
/// <param name="ConversationId">The conversation the request was made in.</param>
/// <param name="SourceMessageId">The guest message that triggered it; at most one request per message, so
/// reprocessing the message cannot create a duplicate delivery (Journey 9, P8).</param>
public sealed record RequestOrigin(
    GuestId? GuestId = null,
    StayContextId? StayContextId = null,
    ConversationId? ConversationId = null,
    GuestMessageId? SourceMessageId = null)
{
    /// <summary>
    /// A request raised by staff, with no guest conversation behind it.
    /// </summary>
    public static RequestOrigin Staff { get; } = new();

    /// <summary>
    /// Whether a guest asked for it (through a conversation or as an identified guest).
    /// </summary>
    public bool IsGuestInitiated => GuestId is not null || ConversationId is not null;
}
