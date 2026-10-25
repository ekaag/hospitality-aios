namespace HospitalityOS.Domain.Properties.AccommodationTypes;

/// <summary>
/// An accommodation type's descriptive information. Validated and normalized by <see cref="AccommodationType"/>.
/// </summary>
/// <param name="Name">Display name, e.g. <c>Deluxe King</c>; required.</param>
/// <param name="MaxOccupancy">Most guests the type sleeps, from 1 to <see cref="AccommodationType.MaxAllowedOccupancy"/>.</param>
/// <param name="Description">What the type offers, e.g. bed configuration and view; optional.</param>
public sealed record AccommodationTypeDetails(string Name, int MaxOccupancy, string? Description = null);
