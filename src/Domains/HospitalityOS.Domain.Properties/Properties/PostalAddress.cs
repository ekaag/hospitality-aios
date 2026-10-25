namespace HospitalityOS.Domain.Properties.Properties;

/// <summary>
/// A property's street address.
/// </summary>
/// <param name="Line1">First address line; required.</param>
/// <param name="Line2">Second address line; optional.</param>
/// <param name="City">City or locality; required.</param>
/// <param name="Region">State, province, or region; optional.</param>
/// <param name="PostalCode">Postal or ZIP code; optional.</param>
/// <param name="CountryCode">ISO 3166-1 alpha-2 country code, e.g. <c>US</c>; required.</param>
public sealed record PostalAddress(
    string Line1,
    string? Line2,
    string City,
    string? Region,
    string? PostalCode,
    string CountryCode);
