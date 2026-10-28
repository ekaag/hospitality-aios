using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Contracts.Api.Spaces;

/// <summary>
/// A space, normalized (trimmed).
/// </summary>
/// <param name="Id">Platform-owned ID.</param>
/// <param name="PropertyId">The property.</param>
/// <param name="ParentSpaceId">The containing space; <c>null</c> for a top-level space.</param>
/// <param name="Name">Display name.</param>
/// <param name="Kind">What kind of place it is.</param>
/// <param name="Code">Short label staff use.</param>
/// <param name="Description">What the space is.</param>
/// <param name="Directions">How a guest gets there.</param>
/// <param name="Version">Increases with every change; send it back in <c>If-Match</c> to update safely.</param>
public sealed record SpaceResponse(
    SpaceId Id,
    PropertyId PropertyId,
    SpaceId? ParentSpaceId,
    string Name,
    SpaceKind Kind,
    string? Code,
    string? Description,
    string? Directions,
    long Version);
