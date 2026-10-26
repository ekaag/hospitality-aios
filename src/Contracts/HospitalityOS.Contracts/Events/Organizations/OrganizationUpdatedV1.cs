using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Contracts.Events.Organizations;

/// <summary>
/// Version 1 of <c>OrganizationUpdated</c>: an organization was renamed.
/// </summary>
/// <param name="OrganizationId">The organization.</param>
/// <param name="Name">The name.</param>
public sealed record OrganizationUpdatedV1(
    OrganizationId OrganizationId,
    string Name) : IEventPayload;
