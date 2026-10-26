using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Contracts.Events.Values;

/// <summary>
/// A restaurant's information.
/// </summary>
/// <param name="Name">Display name.</param>
/// <param name="Cuisine">Cuisine.</param>
/// <param name="Description">Description.</param>
/// <param name="LocationSpaceId">Where it is.</param>
public sealed record RestaurantDetailsData(
    string Name,
    string? Cuisine,
    string? Description,
    SpaceId? LocationSpaceId);
