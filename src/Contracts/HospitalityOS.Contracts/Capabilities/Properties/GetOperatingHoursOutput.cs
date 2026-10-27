namespace HospitalityOS.Contracts.Capabilities.Properties;

/// <summary>
/// Output of <c>Property.GetOperatingHours</c>.
/// </summary>
/// <param name="Subject">Whose hours these are.</param>
/// <param name="TimeZone">IANA time zone of the property.</param>
/// <param name="LocalNow">The current property-local time.</param>
/// <param name="Hours">The hours on the date asked for.</param>
public sealed record GetOperatingHoursOutput(
    Subject Subject,
    string TimeZone,
    DateTime LocalNow,
    HoursOnDate Hours);
