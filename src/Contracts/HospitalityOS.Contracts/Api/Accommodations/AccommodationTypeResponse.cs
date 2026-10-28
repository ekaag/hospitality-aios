using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Contracts.Api.Accommodations;

/// <summary>
/// A kind of room or suite, normalized (trimmed).
/// </summary>
/// <param name="Id">Platform-owned ID.</param>
/// <param name="PropertyId">The property.</param>
/// <param name="Name">Display name.</param>
/// <param name="MaxOccupancy">Most guests it sleeps.</param>
/// <param name="Description">What it offers.</param>
/// <param name="Version">Increases with every change.</param>
public sealed record AccommodationTypeResponse(
    Guid Id,
    PropertyId PropertyId,
    string Name,
    int MaxOccupancy,
    string? Description,
    long Version);
