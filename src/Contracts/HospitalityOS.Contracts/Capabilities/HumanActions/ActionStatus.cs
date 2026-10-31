namespace HospitalityOS.Contracts.Capabilities.HumanActions;

/// <summary>
/// Where a human action is, canonically.
/// </summary>
public enum ActionStatus
{
    /// <summary>
    /// Requested; nobody is assigned yet.
    /// </summary>
    Requested,

    /// <summary>
    /// Assigned to a staff member, who has not accepted yet.
    /// </summary>
    Assigned,

    /// <summary>
    /// The assignee accepted it.
    /// </summary>
    Accepted,

    /// <summary>
    /// The assignee is doing it.
    /// </summary>
    InProgress,

    /// <summary>
    /// Done.
    /// </summary>
    Completed,

    /// <summary>
    /// The assignee could not do it.
    /// </summary>
    Failed,

    /// <summary>
    /// No longer needed.
    /// </summary>
    Cancelled,

    /// <summary>
    /// Its deadline passed before work started.
    /// </summary>
    Expired,
}
