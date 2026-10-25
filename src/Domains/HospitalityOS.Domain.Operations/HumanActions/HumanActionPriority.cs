namespace HospitalityOS.Domain.Operations.HumanActions;

/// <summary>
/// How urgently a <see cref="HumanAction"/> should be done; shown on the staff card.
/// </summary>
public enum HumanActionPriority
{
    /// <summary>
    /// When convenient.
    /// </summary>
    Low,

    /// <summary>
    /// The default for guest requests.
    /// </summary>
    Normal,

    /// <summary>
    /// Ahead of normal work.
    /// </summary>
    High,

    /// <summary>
    /// Immediately, e.g. safety issues.
    /// </summary>
    Urgent,
}
