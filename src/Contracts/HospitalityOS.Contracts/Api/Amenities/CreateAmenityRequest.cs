using HospitalityOS.Contracts.Capabilities.Properties;
using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Contracts.Api.Amenities;

/// <summary>
/// <c>POST /api/v1/amenities</c>: adds a facility guests can use, e.g. the pool (Journey 2). Its hours and rules are
/// separate operating hours and policy records about it.
/// Every field may be omitted: missing required fields are reported by validation, field by field.
/// </summary>
/// <param name="PropertyId">The property offering it; required.</param>
/// <param name="Name">Display name, e.g. <c>Fitness Center</c>; required.</param>
/// <param name="Category">Broad grouping; required.</param>
/// <param name="Description">What it offers.</param>
/// <param name="LocationSpaceId">Where it is: a space in the same property.</param>
public sealed record CreateAmenityRequest(
    PropertyId? PropertyId = null,
    string? Name = null,
    AmenityCategory? Category = null,
    string? Description = null,
    SpaceId? LocationSpaceId = null);
