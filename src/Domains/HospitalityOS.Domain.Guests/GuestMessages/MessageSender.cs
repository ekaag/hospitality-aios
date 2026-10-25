namespace HospitalityOS.Domain.Guests.GuestMessages;

/// <summary>
/// Who wrote a <see cref="GuestMessage"/>.
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
    /// An automatic notification, e.g. "your towels have been delivered".
    /// </summary>
    System,
}
