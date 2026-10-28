namespace HospitalityOS.Contracts.Api.Properties;

/// <summary>
/// <c>PUT /api/v1/properties/{id}</c>: replaces a property's base information. The owning organization does not change.
/// Every field may be omitted: missing required fields are reported by validation, field by field.
/// </summary>
/// <param name="Name">Display name; required.</param>
/// <param name="TimeZone">IANA time zone; required.</param>
/// <param name="Address">Street address; required.</param>
/// <param name="ContactPhone">Front-desk phone number.</param>
/// <param name="ContactEmail">Front-desk email address.</param>
public sealed record UpdatePropertyRequest(
    string? Name = null,
    string? TimeZone = null,
    PropertyAddress? Address = null,
    string? ContactPhone = null,
    string? ContactEmail = null);
