using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Contracts.Capabilities.Properties;

/// <summary>
/// An amenity of the property.
/// </summary>
/// <param name="Source">The amenity record; its ID is the amenity ID.</param>
/// <param name="Name">Name.</param>
/// <param name="Category">Kind.</param>
/// <param name="Description">Description.</param>
/// <param name="LocationSpaceId">Where it is.</param>
public sealed record AmenityEntry(
    SourceReference Source,
    string Name,
    AmenityCategory Category,
    string? Description,
    SpaceId? LocationSpaceId);
