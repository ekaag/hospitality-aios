using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Contracts.Api.Restaurants;

/// <summary>
/// <c>POST /api/v1/restaurants</c>: adds a place to eat or drink (Journey 3). Its menus are separate menu records, and
/// its opening hours an operating hours record about it.
/// Every field may be omitted: missing required fields are reported by validation, field by field.
/// </summary>
/// <param name="PropertyId">The property; required.</param>
/// <param name="Name">Display name, e.g. <c>Harbour Grill</c>; required.</param>
/// <param name="Cuisine">Cuisine, e.g. <c>Seafood</c>.</param>
/// <param name="Description">What it offers.</param>
/// <param name="LocationSpaceId">Where it is: a space in the same property.</param>
public sealed record CreateRestaurantRequest(
    PropertyId? PropertyId = null,
    string? Name = null,
    string? Cuisine = null,
    string? Description = null,
    SpaceId? LocationSpaceId = null);
