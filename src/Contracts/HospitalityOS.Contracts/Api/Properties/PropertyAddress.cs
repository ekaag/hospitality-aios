namespace HospitalityOS.Contracts.Api.Properties;

/// <summary>
/// A property's street address.
/// </summary>
/// <param name="Line1">First address line; required.</param>
/// <param name="Line2">Second address line.</param>
/// <param name="City">City or locality; required.</param>
/// <param name="Region">State, province, or region.</param>
/// <param name="PostalCode">Postal or ZIP code.</param>
/// <param name="CountryCode">ISO 3166-1 alpha-2 country code, e.g. <c>GB</c>; required.</param>
public sealed record PropertyAddress(
    string? Line1 = null,
    string? Line2 = null,
    string? City = null,
    string? Region = null,
    string? PostalCode = null,
    string? CountryCode = null);
