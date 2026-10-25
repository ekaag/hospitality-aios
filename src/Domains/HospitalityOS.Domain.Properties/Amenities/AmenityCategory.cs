namespace HospitalityOS.Domain.Properties.Amenities;

/// <summary>
/// Broad grouping of an <see cref="Amenity"/>, used for discovery ("what can I do here?").
/// </summary>
public enum AmenityCategory
{
    /// <summary>
    /// Pools and hot tubs.
    /// </summary>
    Pool,

    /// <summary>
    /// Gym and fitness facilities.
    /// </summary>
    Fitness,

    /// <summary>
    /// Spa and wellness.
    /// </summary>
    Spa,

    /// <summary>
    /// Business center, meeting rooms.
    /// </summary>
    Business,

    /// <summary>
    /// Kids' club, play areas.
    /// </summary>
    Family,

    /// <summary>
    /// Games, sports, leisure.
    /// </summary>
    Recreation,

    /// <summary>
    /// Parking and EV charging.
    /// </summary>
    Parking,

    /// <summary>
    /// Wi-Fi and connectivity.
    /// </summary>
    Connectivity,

    /// <summary>
    /// Anything else.
    /// </summary>
    Other,
}
