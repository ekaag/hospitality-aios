using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Contracts.Api.Spaces;

/// <summary>
/// <c>POST /api/v1/spaces</c>: adds a place to a property's hierarchy, e.g. Main Building → Floor 4 → Room 412.
/// Every field may be omitted: missing required fields are reported by validation, field by field.
/// </summary>
/// <param name="PropertyId">The property; required.</param>
/// <param name="ParentSpaceId">The containing space in the same property; omit for a top-level space. Fixed once
/// created.</param>
/// <param name="Name">Display name, e.g. <c>Pool Deck</c>; required.</param>
/// <param name="Kind">What kind of place it is; required.</param>
/// <param name="Code">Short label staff use, e.g. <c>412</c>.</param>
/// <param name="Description">What the space is.</param>
/// <param name="Directions">How a guest gets there.</param>
public sealed record CreateSpaceRequest(
    PropertyId? PropertyId = null,
    SpaceId? ParentSpaceId = null,
    string? Name = null,
    SpaceKind? Kind = null,
    string? Code = null,
    string? Description = null,
    string? Directions = null);
