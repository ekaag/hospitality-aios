namespace HospitalityOS.Domain.Activities.PropertyEvents;

/// <summary>
/// Whether a <see cref="PropertyEvent"/> is still going ahead.
/// </summary>
public enum PropertyEventStatus
{
    /// <summary>
    /// Going ahead as scheduled.
    /// </summary>
    Scheduled,

    /// <summary>
    /// Called off; never offered to guests, and cannot be edited.
    /// </summary>
    Cancelled,
}
