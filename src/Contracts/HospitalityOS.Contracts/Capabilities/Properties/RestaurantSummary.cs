namespace HospitalityOS.Contracts.Capabilities.Properties;

/// <summary>
/// A restaurant or bar of the property.
/// </summary>
/// <param name="Source">The restaurant record; its ID is the restaurant ID.</param>
/// <param name="Name">Name.</param>
/// <param name="Cuisine">Cuisine.</param>
public sealed record RestaurantSummary(
    SourceReference Source,
    string Name,
    string? Cuisine);
