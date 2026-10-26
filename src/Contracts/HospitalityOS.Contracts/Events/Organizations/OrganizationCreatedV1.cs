using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Contracts.Events.Organizations;

/// <summary>
/// Version 1 of <c>OrganizationCreated</c>: an organization was created.
/// </summary>
/// <param name="OrganizationId">The organization.</param>
/// <param name="Name">The name.</param>
public sealed record OrganizationCreatedV1(
    OrganizationId OrganizationId,
    string Name) : IEventPayload;
