using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Contracts.Api.Stays;

/// <summary>
/// A stay that is reserved or in house, as the front desk sees it.
/// </summary>
/// <param name="Id">The stay.</param>
/// <param name="GuestId">The guest.</param>
/// <param name="GuestName">How the guest is addressed, if known.</param>
/// <param name="UnitNumber">The room, once assigned.</param>
/// <param name="Arrival">The arrival date.</param>
/// <param name="Departure">The departure date.</param>
/// <param name="Status">Where the stay is.</param>
/// <param name="HasGuestLink">Whether it has an open check-in link.</param>
public sealed record StayResponse(
    StayContextId Id,
    GuestId GuestId,
    string? GuestName,
    string? UnitNumber,
    DateOnly Arrival,
    DateOnly Departure,
    StayStatus Status,
    bool HasGuestLink);
