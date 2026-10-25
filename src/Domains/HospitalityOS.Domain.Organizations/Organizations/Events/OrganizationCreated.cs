using HospitalityOS.SharedKernel.Domain;
using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Domain.Organizations.Organizations.Events;

/// <summary>
/// An organization was created.
/// </summary>
public sealed record OrganizationCreated(OrganizationId OrganizationId, string Name, DateTimeOffset OccurredAt) : IDomainEvent;
