using HospitalityOS.SharedKernel.Domain;
using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Domain.Organizations.Organizations.Events;

/// <summary>
/// An organization's details changed. Carries the full new state, not a diff.
/// </summary>
public sealed record OrganizationUpdated(OrganizationId OrganizationId, string Name, DateTimeOffset OccurredAt) : IDomainEvent;
