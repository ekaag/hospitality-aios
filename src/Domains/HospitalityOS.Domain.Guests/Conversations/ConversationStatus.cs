namespace HospitalityOS.Domain.Guests.Conversations;

/// <summary>
/// Whether a <see cref="Conversation"/> accepts messages.
/// </summary>
public enum ConversationStatus
{
    /// <summary>
    /// Accepting messages.
    /// </summary>
    Open,

    /// <summary>
    /// Ended; no further messages. Final.
    /// </summary>
    Closed,
}
