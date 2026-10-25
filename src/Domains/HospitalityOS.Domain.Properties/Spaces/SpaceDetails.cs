namespace HospitalityOS.Domain.Properties.Spaces;

/// <summary>
/// A space's descriptive information. Validated and normalized by <see cref="Space"/>.
/// </summary>
/// <param name="Name">Display name, e.g. <c>Pool Deck</c>; required.</param>
/// <param name="Kind">What kind of place it is.</param>
/// <param name="Code">Short label staff use, e.g. <c>412</c> or <c>L3</c>; optional.</param>
/// <param name="Description">What the space is; optional.</param>
/// <param name="Directions">How a guest gets there, e.g. <c>Take the east elevators to level 3</c>; optional.</param>
public sealed record SpaceDetails(
    string Name,
    SpaceKind Kind,
    string? Code = null,
    string? Description = null,
    string? Directions = null);
