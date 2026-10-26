using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Contracts.Events.Properties;

/// <summary>
/// Version 1 of <c>AccommodationUnitCreated</c>: an accommodation unit (room) was created.
/// </summary>
/// <param name="AccommodationUnitId">The accommodation unit, if any.</param>
/// <param name="PropertyId">The property.</param>
/// <param name="AccommodationTypeId">The accommodation type.</param>
/// <param name="SpaceId">The space.</param>
/// <param name="UnitNumber">The unit number guests and staff use.</param>
public sealed record AccommodationUnitCreatedV1(
    AccommodationUnitId AccommodationUnitId,
    PropertyId PropertyId,
    Guid AccommodationTypeId,
    SpaceId SpaceId,
    string UnitNumber) : IEventPayload;
