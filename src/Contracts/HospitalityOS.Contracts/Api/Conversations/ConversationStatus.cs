namespace HospitalityOS.Contracts.Api.Conversations;

/// <summary>
/// Whether a conversation accepts messages.
/// </summary>
public enum ConversationStatus
{
    /// <summary>
    /// Accepts messages.
    /// </summary>
    Open,

    /// <summary>
    /// Ended; start a new one.
    /// </summary>
    Closed,
}
