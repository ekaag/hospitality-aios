using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Contracts.Api.Accommodations;

/// <summary>
/// Adds a room guests stay in, e.g. room 412, of a type and at a space of the same property. The PMS names rooms by their
/// number, so numbers are unique within the property.
/// Every field may be omitted: missing required fields are reported by validation, field by field.
/// </summary>
/// <param name="AccommodationTypeId">The room's type, which determines the property; required.</param>
/// <param name="SpaceId">Where the room is, a space in the same property; required.</param>
/// <param name="UnitNumber">The number guests and staff use, e.g. <c>412</c>; required.</param>
public sealed record CreateAccommodationUnitRequest(
    Guid? AccommodationTypeId = null,
    SpaceId? SpaceId = null,
    string? UnitNumber = null);
