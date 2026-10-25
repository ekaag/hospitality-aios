namespace HospitalityOS.Domain.Operations.HumanActions;

/// <summary>
/// The state of a <see cref="HumanAction"/> (spec §7). Allowed transitions:
/// <code>
/// Requested ─assign→ Assigned ─accept→ Accepted ─start→ InProgress ─complete→ Completed
///     │                │ ▲reassign       │ complete──────────────────────────↗
///     │                │                 │
///     ├─ cancel / expire (Requested, Assigned, Accepted) → Cancelled / Expired
///     └─ fail (Assigned, Accepted, InProgress) → Failed
/// </code>
/// Completed, Failed, Cancelled, and Expired are final.
/// </summary>
public enum HumanActionStatus
{
    /// <summary>
    /// Created; waiting for the router to assign someone.
    /// </summary>
    Requested,

    /// <summary>
    /// Assigned to a staff member, who has not yet accepted.
    /// </summary>
    Assigned,

    /// <summary>
    /// The assignee accepted the work.
    /// </summary>
    Accepted,

    /// <summary>
    /// The assignee is doing the work.
    /// </summary>
    InProgress,

    /// <summary>
    /// Done. Final.
    /// </summary>
    Completed,

    /// <summary>
    /// The assignee could not do it, e.g. could not access the room. Final.
    /// </summary>
    Failed,

    /// <summary>
    /// No longer needed. Final.
    /// </summary>
    Cancelled,

    /// <summary>
    /// The deadline passed before work started. Final.
    /// </summary>
    Expired,
}
