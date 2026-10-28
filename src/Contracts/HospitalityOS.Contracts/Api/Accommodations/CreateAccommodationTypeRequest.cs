using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Contracts.Api.Accommodations;

/// <summary>
/// Adds a kind of room or suite to a property, e.g. Deluxe King. Its rooms are accommodation units.
/// Every field may be omitted: missing required fields are reported by validation, field by field.
/// </summary>
/// <param name="PropertyId">The property; required.</param>
/// <param name="Name">Display name, e.g. <c>Deluxe King</c>; required.</param>
/// <param name="MaxOccupancy">Most guests it sleeps, 1 to 50; required.</param>
/// <param name="Description">What it offers.</param>
public sealed record CreateAccommodationTypeRequest(
    PropertyId? PropertyId = null,
    string? Name = null,
    int? MaxOccupancy = null,
    string? Description = null);
