using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Contracts.Api.Organizations;

/// <summary>
/// An organization: a company that owns or operates properties.
/// </summary>
/// <param name="Id">Platform-owned ID.</param>
/// <param name="Name">Display name.</param>
/// <param name="Version">Increases with every change; send it back in <c>If-Match</c> to update safely.</param>
public sealed record OrganizationResponse(OrganizationId Id, string Name, long Version);
