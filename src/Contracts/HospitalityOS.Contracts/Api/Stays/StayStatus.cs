namespace HospitalityOS.Contracts.Api.Stays;

/// <summary>
/// Where a stay is.
/// </summary>
public enum StayStatus
{
    /// <summary>
    /// Booked; not arrived.
    /// </summary>
    Reserved,

    /// <summary>
    /// Checked in.
    /// </summary>
    InHouse,
}
