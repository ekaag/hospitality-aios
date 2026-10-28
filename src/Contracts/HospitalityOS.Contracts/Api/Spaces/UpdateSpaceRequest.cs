namespace HospitalityOS.Contracts.Api.Spaces;

/// <summary>
/// <c>PUT /api/v1/spaces/{id}</c>: replaces a space's descriptive information. Its property and parent do not change.
/// Every field may be omitted: missing required fields are reported by validation, field by field.
/// </summary>
/// <param name="Name">Display name; required.</param>
/// <param name="Kind">What kind of place it is; required.</param>
/// <param name="Code">Short label staff use.</param>
/// <param name="Description">What the space is.</param>
/// <param name="Directions">How a guest gets there.</param>
public sealed record UpdateSpaceRequest(
    string? Name = null,
    SpaceKind? Kind = null,
    string? Code = null,
    string? Description = null,
    string? Directions = null);
