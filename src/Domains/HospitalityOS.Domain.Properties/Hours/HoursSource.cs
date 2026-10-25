namespace HospitalityOS.Domain.Properties.Hours;

/// <summary>
/// Which rule decided a date's hours, so answers can explain themselves ("closed today for resurfacing").
/// </summary>
public enum HoursSource
{
    /// <summary>
    /// The regular weekly hours.
    /// </summary>
    Weekly,

    /// <summary>
    /// A date override.
    /// </summary>
    Override,

    /// <summary>
    /// A temporary closure.
    /// </summary>
    Closure,
}
