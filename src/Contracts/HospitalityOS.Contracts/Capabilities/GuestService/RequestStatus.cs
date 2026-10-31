namespace HospitalityOS.Contracts.Capabilities.GuestService;

/// <summary>
/// Where a service request is, canonically; report it as it is, never as an estimate.
/// </summary>
public enum RequestStatus
{
    /// <summary>
    /// Received; nobody has started on it yet.
    /// </summary>
    Open,

    /// <summary>
    /// Someone is working on it.
    /// </summary>
    InProgress,

    /// <summary>
    /// The work could not be done as planned; staff are resolving it.
    /// </summary>
    Exception,

    /// <summary>
    /// Done.
    /// </summary>
    Completed,

    /// <summary>
    /// Withdrawn.
    /// </summary>
    Cancelled,
}
