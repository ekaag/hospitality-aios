namespace HospitalityOS.Domain.Services.ServiceRequests;

/// <summary>
/// Where a <see cref="ServiceRequest"/> stands — the canonical status the concierge reports (Journey 12).
/// </summary>
public enum ServiceRequestStatus
{
    /// <summary>
    /// Received; work not yet started.
    /// </summary>
    Open,

    /// <summary>
    /// Someone is working on it.
    /// </summary>
    InProgress,

    /// <summary>
    /// Work failed or stalled; still unresolved and visible in the exceptions queue (Journey 20).
    /// </summary>
    Exception,

    /// <summary>
    /// Fulfilled. Final.
    /// </summary>
    Completed,

    /// <summary>
    /// Withdrawn. Final.
    /// </summary>
    Cancelled,
}
