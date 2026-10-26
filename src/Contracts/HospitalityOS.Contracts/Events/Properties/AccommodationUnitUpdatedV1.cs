using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Contracts.Events.Properties;

/// <summary>
/// Version 1 of <c>AccommodationUnitUpdated</c>: an accommodation unit was renumbered or changed type.
/// </summary>
/// <param name="AccommodationUnitId">The accommodation unit, if any.</param>
/// <param name="AccommodationTypeId">The accommodation type.</param>
/// <param name="UnitNumber">The unit number guests and staff use.</param>
public sealed record AccommodationUnitUpdatedV1(
    AccommodationUnitId AccommodationUnitId,
    Guid AccommodationTypeId,
    string UnitNumber) : IEventPayload;
