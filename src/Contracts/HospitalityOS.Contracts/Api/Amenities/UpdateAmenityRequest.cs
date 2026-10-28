using HospitalityOS.Contracts.Capabilities.Properties;
using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Contracts.Api.Amenities;

/// <summary>
/// <c>PUT /api/v1/amenities/{id}</c>: replaces an amenity's information and location; omitting the location clears it.
/// Every field may be omitted: missing required fields are reported by validation, field by field.
/// </summary>
/// <param name="Name">Display name; required.</param>
/// <param name="Category">Broad grouping; required.</param>
/// <param name="Description">What it offers.</param>
/// <param name="LocationSpaceId">Where it is: a space in the same property.</param>
public sealed record UpdateAmenityRequest(
    string? Name = null,
    AmenityCategory? Category = null,
    string? Description = null,
    SpaceId? LocationSpaceId = null);
