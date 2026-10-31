namespace HospitalityOS.Contracts.Api.Work;

/// <summary>
/// The staff surface's views of a property's work (spec §32). A worker sees their own work; managers and administrators
/// see everyone's.
/// </summary>
public enum WorkView
{
    /// <summary>
    /// Assigned work not yet done: a worker's "My Actions"; for managers, everyone's.
    /// </summary>
    Assigned,

    /// <summary>
    /// Work waiting for someone: for a worker, only what they may claim.
    /// </summary>
    Unassigned,

    /// <summary>
    /// Work completed in the last day, most recent first.
    /// </summary>
    Completed,

    /// <summary>
    /// Work that failed, expired, or was cancelled while its request is still waiting for new work.
    /// </summary>
    Exceptions,
}
