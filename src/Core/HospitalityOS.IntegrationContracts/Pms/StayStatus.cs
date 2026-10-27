namespace HospitalityOS.IntegrationContracts.Pms;

/// <summary>
/// Lifecycle of a <see cref="PmsStay"/>, named as the canonical stay status.
/// </summary>
public enum StayStatus
{
    /// <summary>
    /// Booked, not yet arrived.
    /// </summary>
    Reserved,

    /// <summary>
    /// Checked in and staying.
    /// </summary>
    InHouse,

    /// <summary>
    /// Departed.
    /// </summary>
    CheckedOut,

    /// <summary>
    /// Called off before arrival.
    /// </summary>
    Cancelled,
}
