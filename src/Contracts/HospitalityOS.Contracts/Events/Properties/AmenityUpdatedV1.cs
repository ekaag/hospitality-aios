using HospitalityOS.Contracts.Events.Values;
using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Contracts.Events.Properties;

/// <summary>
/// Version 1 of <c>AmenityUpdated</c>: an amenity changed.
/// </summary>
/// <param name="AmenityId">The amenity.</param>
/// <param name="Details">The full, normalized details after the change.</param>
/// <param name="LocationSpaceId">Where it is, if known.</param>
public sealed record AmenityUpdatedV1(
    Guid AmenityId,
    AmenityDetailsData Details,
    SpaceId? LocationSpaceId) : IEventPayload;
