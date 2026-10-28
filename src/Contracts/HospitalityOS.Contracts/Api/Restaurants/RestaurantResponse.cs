using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Contracts.Api.Restaurants;

/// <summary>
/// A restaurant, normalized (trimmed).
/// </summary>
/// <param name="Id">Platform-owned ID.</param>
/// <param name="PropertyId">The property.</param>
/// <param name="Name">Display name.</param>
/// <param name="Cuisine">Cuisine.</param>
/// <param name="Description">What it offers.</param>
/// <param name="LocationSpaceId">Where it is, if recorded.</param>
/// <param name="Version">Increases with every change; send it back in <c>If-Match</c> to update safely.</param>
public sealed record RestaurantResponse(
    Guid Id,
    PropertyId PropertyId,
    string Name,
    string? Cuisine,
    string? Description,
    SpaceId? LocationSpaceId,
    long Version);
