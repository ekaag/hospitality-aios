namespace HospitalityOS.Contracts.Events.Values;

/// <summary>
/// An amenity's information.
/// </summary>
/// <param name="Name">Display name.</param>
/// <param name="Category">Pool, fitness, ... (domain enum name).</param>
/// <param name="Description">Description.</param>
public sealed record AmenityDetailsData(
    string Name,
    string Category,
    string? Description);
