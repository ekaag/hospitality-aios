namespace HospitalityOS.Contracts.Events.Values;

/// <summary>
/// A property's base information.
/// </summary>
/// <param name="Name">Display name.</param>
/// <param name="TimeZoneId">IANA time zone ID, e.g. <c>Europe/London</c>.</param>
/// <param name="Address">Postal address.</param>
/// <param name="ContactPhone">Public contact phone.</param>
/// <param name="ContactEmail">Public contact email.</param>
public sealed record PropertyDetailsData(
    string Name,
    string TimeZoneId,
    PostalAddressData Address,
    string? ContactPhone,
    string? ContactEmail);
