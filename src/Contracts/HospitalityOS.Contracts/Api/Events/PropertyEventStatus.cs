namespace HospitalityOS.Contracts.Api.Events;

/// <summary>
/// Whether an event is going ahead.
/// </summary>
public enum PropertyEventStatus
{
    /// <summary>
    /// Going ahead; offered to guests until it ends.
    /// </summary>
    Scheduled,

    /// <summary>
    /// Called off; never offered to guests, and cannot be edited.
    /// </summary>
    Cancelled,
}
