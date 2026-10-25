using HospitalityOS.SharedKernel.Domain;
using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Domain.Properties.Amenities.Events;

/// <summary>
/// An amenity changed (spec §8). Carries the full new state, not a diff.
/// </summary>
public sealed record AmenityUpdated(
    AmenityId AmenityId,
    AmenityDetails Details,
    SpaceId? LocationSpaceId,
    DateTimeOffset OccurredAt) : IDomainEvent;
