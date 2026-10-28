using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Contracts.Api.Properties;

/// <summary>
/// <c>POST /api/v1/properties</c>: creates a property (one hotel) for an organization in the caller's tenant (Journey 1).
/// Missing or invalid fields are all reported at once.
/// Every field may be omitted: missing required fields are reported by validation, field by field.
/// </summary>
/// <param name="OrganizationId">The owning organization; required.</param>
/// <param name="Name">Display name, e.g. <c>Aurora Grand</c>; required.</param>
/// <param name="TimeZone">IANA time zone, e.g. <c>Europe/London</c>; required. Local times such as hours use it.</param>
/// <param name="Address">Street address; required.</param>
/// <param name="ContactPhone">Front-desk phone number.</param>
/// <param name="ContactEmail">Front-desk email address.</param>
public sealed record CreatePropertyRequest(
    OrganizationId? OrganizationId = null,
    string? Name = null,
    string? TimeZone = null,
    PropertyAddress? Address = null,
    string? ContactPhone = null,
    string? ContactEmail = null);
