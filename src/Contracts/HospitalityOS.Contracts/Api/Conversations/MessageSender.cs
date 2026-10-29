namespace HospitalityOS.Contracts.Api.Conversations;

/// <summary>
/// Who wrote a message.
/// </summary>
public enum MessageSender
{
    /// <summary>
    /// The guest.
    /// </summary>
    Guest,

    /// <summary>
    /// The AI concierge.
    /// </summary>
    Concierge,

    /// <summary>
    /// A staff member.
    /// </summary>
    Staff,

    /// <summary>
    /// An automatic notification.
    /// </summary>
    System,
}
