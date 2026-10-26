namespace HospitalityOS.Contracts.Events.Values;

/// <summary>
/// A postal address.
/// </summary>
/// <param name="Line1">First address line.</param>
/// <param name="Line2">Second address line.</param>
/// <param name="City">City.</param>
/// <param name="Region">Region or state.</param>
/// <param name="PostalCode">Postal code.</param>
/// <param name="CountryCode">ISO 3166-1 alpha-2 country code.</param>
public sealed record PostalAddressData(
    string Line1,
    string? Line2,
    string City,
    string? Region,
    string? PostalCode,
    string CountryCode);
