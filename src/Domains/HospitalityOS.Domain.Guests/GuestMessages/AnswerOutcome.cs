namespace HospitalityOS.Domain.Guests.GuestMessages;

/// <summary>
/// How the concierge answered a guest's question.
/// </summary>
public enum AnswerOutcome
{
    /// <summary>
    /// From an authoritative source.
    /// </summary>
    Answered,

    /// <summary>
    /// No authoritative source answers it, and the guest was told so (Journey 17).
    /// </summary>
    NoAuthoritativeSource,

    /// <summary>
    /// From relevant sources that do not clearly settle it, and the guest was told so (Journey 6).
    /// </summary>
    Uncertain,
}
