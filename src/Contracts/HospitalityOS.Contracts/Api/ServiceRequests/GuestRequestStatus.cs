namespace HospitalityOS.Contracts.Api.ServiceRequests;

/// <summary>
/// Where a guest's request is, as the platform knows it; never an estimate of when it will be done.
/// </summary>
public enum GuestRequestStatus
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
}
