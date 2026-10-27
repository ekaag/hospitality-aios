using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Contracts.Capabilities.Dining;

/// <summary>
/// A restaurant or bar.
/// </summary>
/// <param name="Source">The restaurant record; its ID is the restaurant ID.</param>
/// <param name="Name">Name.</param>
/// <param name="Cuisine">Cuisine.</param>
/// <param name="Description">Description.</param>
/// <param name="LocationSpaceId">Where it is.</param>
/// <param name="HoursToday">Today's hours; absent when none are recorded (unknown, not closed).</param>
public sealed record RestaurantEntry(
    SourceReference Source,
    string Name,
    string? Cuisine,
    string? Description,
    SpaceId? LocationSpaceId,
    HoursOnDate? HoursToday);
