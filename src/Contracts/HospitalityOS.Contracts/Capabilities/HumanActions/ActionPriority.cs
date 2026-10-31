namespace HospitalityOS.Contracts.Capabilities.HumanActions;

/// <summary>
/// How urgently the work should be done.
/// </summary>
public enum ActionPriority
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
