using HospitalityOS.Contracts.Events.Values;
using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Contracts.Events.Properties;

/// <summary>
/// Version 1 of <c>AmenityCreated</c>: an amenity was created (Journey 2).
/// </summary>
/// <param name="AmenityId">The amenity.</param>
/// <param name="PropertyId">The property.</param>
/// <param name="Details">The full, normalized details after the change.</param>
/// <param name="LocationSpaceId">Where it is, if known.</param>
public sealed record AmenityCreatedV1(
    Guid AmenityId,
    PropertyId PropertyId,
    AmenityDetailsData Details,
    SpaceId? LocationSpaceId) : IEventPayload;
