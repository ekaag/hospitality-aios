namespace HospitalityOS.Domain.Properties.Properties;

/// <summary>
/// A property's base information (Journey 1). Validated and normalized by <see cref="Property"/>.
/// </summary>
/// <param name="Name">Display name, e.g. <c>Aurora Grand</c>; required.</param>
/// <param name="TimeZoneId">IANA time zone, e.g. <c>America/New_York</c>; required. Local times such as
/// operating hours are interpreted in this zone.</param>
/// <param name="Address">Street address; required.</param>
/// <param name="ContactPhone">Front-desk phone number; optional.</param>
/// <param name="ContactEmail">Front-desk email address; optional.</param>
public sealed record PropertyDetails(
    string Name,
    string TimeZoneId,
    PostalAddress Address,
    string? ContactPhone = null,
    string? ContactEmail = null);
