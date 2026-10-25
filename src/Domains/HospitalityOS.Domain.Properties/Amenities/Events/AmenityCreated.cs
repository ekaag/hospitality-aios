using HospitalityOS.SharedKernel.Domain;
using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Domain.Properties.Amenities.Events;

/// <summary>
/// An amenity was created (spec §8).
/// </summary>
public sealed record AmenityCreated(
    AmenityId AmenityId,
    PropertyId PropertyId,
    AmenityDetails Details,
    SpaceId? LocationSpaceId,
    DateTimeOffset OccurredAt) : IDomainEvent;
