using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Domain.Dining.Restaurants;

/// <summary>
/// A restaurant's descriptive information (Journey 3). Validated and normalized by <see cref="Restaurant"/>.
/// Opening hours are an <c>OperatingHours</c> record whose subject is this restaurant.
/// </summary>
/// <param name="Name">Display name, e.g. <c>Harbour Grill</c>; required.</param>
/// <param name="Cuisine">Cuisine, e.g. <c>Seafood</c>; optional.</param>
/// <param name="Description">What it offers; optional.</param>
/// <param name="LocationSpaceId">Where it is, a space in the same property (checked by the application layer); optional.</param>
public sealed record RestaurantDetails(
    string Name,
    string? Cuisine = null,
    string? Description = null,
    SpaceId? LocationSpaceId = null);
