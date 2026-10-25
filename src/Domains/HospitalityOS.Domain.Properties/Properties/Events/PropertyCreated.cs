using HospitalityOS.SharedKernel.Domain;
using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Domain.Properties.Properties.Events;

/// <summary>
/// A property was created (spec §8).
/// </summary>
public sealed record PropertyCreated(
    PropertyId PropertyId,
    OrganizationId OrganizationId,
    PropertyDetails Details,
    DateTimeOffset OccurredAt) : IDomainEvent;
