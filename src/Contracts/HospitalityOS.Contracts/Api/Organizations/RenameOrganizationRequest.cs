namespace HospitalityOS.Contracts.Api.Organizations;

/// <summary>
/// <c>PUT /api/v1/organizations/{id}</c>: renames an organization.
/// </summary>
/// <param name="Name">The new display name; required.</param>
public sealed record RenameOrganizationRequest(string? Name = null);
