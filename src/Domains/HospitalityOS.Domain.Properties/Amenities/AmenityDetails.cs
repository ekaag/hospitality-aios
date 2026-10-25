namespace HospitalityOS.Domain.Properties.Amenities;

/// <summary>
/// An amenity's descriptive information. Validated and normalized by <see cref="Amenity"/>.
/// </summary>
/// <param name="Name">Display name, e.g. <c>Fitness Center</c>; required.</param>
/// <param name="Category">Broad grouping.</param>
/// <param name="Description">What it offers; optional.</param>
public sealed record AmenityDetails(string Name, AmenityCategory Category, string? Description = null);
