namespace HospitalityOS.Contracts.Events.Values;

/// <summary>
/// A space's descriptive information.
/// </summary>
/// <param name="Name">Display name.</param>
/// <param name="Kind">Building, floor, room, ... (domain enum name).</param>
/// <param name="Code">Short code, e.g. <c>F4</c>.</param>
/// <param name="Description">Description.</param>
/// <param name="Directions">How guests find it.</param>
public sealed record SpaceDetailsData(
    string Name,
    string Kind,
    string? Code,
    string? Description,
    string? Directions);
