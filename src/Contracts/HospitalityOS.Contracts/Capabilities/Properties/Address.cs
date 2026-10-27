namespace HospitalityOS.Contracts.Capabilities.Properties;

/// <summary>
/// A postal address.
/// </summary>
/// <param name="Line1">First line.</param>
/// <param name="Line2">Second line.</param>
/// <param name="City">City.</param>
/// <param name="Region">Region or state.</param>
/// <param name="PostalCode">Postal code.</param>
/// <param name="CountryCode">ISO 3166-1 alpha-2 country code.</param>
public sealed record Address(
    string Line1,
    string? Line2,
    string City,
    string? Region,
    string? PostalCode,
    string CountryCode);
