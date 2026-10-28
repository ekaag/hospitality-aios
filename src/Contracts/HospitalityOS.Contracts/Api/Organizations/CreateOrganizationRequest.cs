namespace HospitalityOS.Contracts.Api.Organizations;

/// <summary>
/// <c>POST /api/v1/organizations</c>: creates an organization in the caller's tenant.
/// Every field may be omitted: missing required fields are reported by validation, field by field.
/// </summary>
/// <param name="Name">Display name, e.g. <c>Aurora Hotels</c>; required.</param>
public sealed record CreateOrganizationRequest(string? Name = null);
