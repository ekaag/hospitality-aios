namespace HospitalityOS.Contracts.Events.Values;

/// <summary>
/// An accommodation type's information.
/// </summary>
/// <param name="Name">Display name.</param>
/// <param name="MaxOccupancy">Maximum guests.</param>
/// <param name="Description">Description.</param>
public sealed record AccommodationTypeDetailsData(
    string Name,
    int MaxOccupancy,
    string? Description);
