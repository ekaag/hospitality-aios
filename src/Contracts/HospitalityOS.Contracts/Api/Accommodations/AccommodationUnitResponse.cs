using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Contracts.Api.Accommodations;

/// <summary>
/// A room guests stay in, normalized (trimmed).
/// </summary>
/// <param name="Id">Platform-owned ID.</param>
/// <param name="PropertyId">The property.</param>
/// <param name="AccommodationTypeId">The room's type.</param>
/// <param name="SpaceId">Where the room is.</param>
/// <param name="UnitNumber">The number guests and staff use.</param>
/// <param name="Version">Increases with every change.</param>
public sealed record AccommodationUnitResponse(
    AccommodationUnitId Id,
    PropertyId PropertyId,
    Guid AccommodationTypeId,
    SpaceId SpaceId,
    string UnitNumber,
    long Version);
