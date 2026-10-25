namespace HospitalityOS.Domain.Guests.StayContexts;

/// <summary>
/// Lifecycle of a <see cref="StayContext"/>: Reserved → InHouse → CheckedOut, or Reserved → Cancelled.
/// </summary>
public enum StayStatus
{
    /// <summary>
    /// Booked, not yet arrived.
    /// </summary>
    Reserved,

    /// <summary>
    /// Checked in and staying; service requests can be delivered to the unit.
    /// </summary>
    InHouse,

    /// <summary>
    /// Departed. Final.
    /// </summary>
    CheckedOut,

    /// <summary>
    /// Called off before arrival. Final.
    /// </summary>
    Cancelled,
}
