using HospitalityOS.SharedKernel.Domain;
using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Domain.Properties.Spaces.Events;

/// <summary>
/// A space was created.
/// </summary>
public sealed record SpaceCreated(
    SpaceId SpaceId,
    PropertyId PropertyId,
    SpaceId? ParentSpaceId,
    SpaceDetails Details,
    DateTimeOffset OccurredAt) : IDomainEvent;
