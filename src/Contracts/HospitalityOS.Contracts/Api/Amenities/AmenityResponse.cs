using HospitalityOS.Contracts.Capabilities.Properties;
using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Contracts.Api.Amenities;

/// <summary>
/// An amenity, normalized (trimmed).
/// </summary>
/// <param name="Id">Platform-owned ID.</param>
/// <param name="PropertyId">The property offering it.</param>
/// <param name="Name">Display name.</param>
/// <param name="Category">Broad grouping.</param>
/// <param name="Description">What it offers.</param>
/// <param name="LocationSpaceId">Where it is, if recorded.</param>
/// <param name="Version">Increases with every change; send it back in <c>If-Match</c> to update safely.</param>
public sealed record AmenityResponse(
    Guid Id,
    PropertyId PropertyId,
    string Name,
    AmenityCategory Category,
    string? Description,
    SpaceId? LocationSpaceId,
    long Version);
