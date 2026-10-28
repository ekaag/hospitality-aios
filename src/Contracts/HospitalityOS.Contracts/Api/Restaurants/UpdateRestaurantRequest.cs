using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Contracts.Api.Restaurants;

/// <summary>
/// <c>PUT /api/v1/restaurants/{id}</c>: replaces a restaurant's information; omitting an optional field clears it.
/// Every field may be omitted: missing required fields are reported by validation, field by field.
/// </summary>
/// <param name="Name">Display name; required.</param>
/// <param name="Cuisine">Cuisine.</param>
/// <param name="Description">What it offers.</param>
/// <param name="LocationSpaceId">Where it is: a space in the same property.</param>
public sealed record UpdateRestaurantRequest(
    string? Name = null,
    string? Cuisine = null,
    string? Description = null,
    SpaceId? LocationSpaceId = null);
