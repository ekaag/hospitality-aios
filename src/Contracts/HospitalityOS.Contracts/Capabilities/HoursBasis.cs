namespace HospitalityOS.Contracts.Capabilities;

/// <summary>
/// Which rule decided the hours of a date.
/// </summary>
public enum HoursBasis
{
    /// <summary>
    /// The regular weekly hours.
    /// </summary>
    Weekly,

    /// <summary>
    /// Special hours for that date, e.g. a holiday.
    /// </summary>
    Override,

    /// <summary>
    /// A temporary closure; closed all day.
    /// </summary>
    Closure,
}
