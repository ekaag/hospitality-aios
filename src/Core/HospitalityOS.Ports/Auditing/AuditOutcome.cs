namespace HospitalityOS.Ports.Auditing;

/// <summary>
/// How an audited action ended.
/// </summary>
public enum AuditOutcome
{
    /// <summary>
    /// The action happened.
    /// </summary>
    Succeeded,

    /// <summary>
    /// The action was attempted and failed (validation, state, or infrastructure).
    /// </summary>
    Failed,

    /// <summary>
    /// The actor was not allowed to perform the action.
    /// </summary>
    Denied,
}
