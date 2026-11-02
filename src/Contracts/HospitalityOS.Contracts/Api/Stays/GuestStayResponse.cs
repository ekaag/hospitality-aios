using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Contracts.Api.Stays;

/// <summary>
/// The guest's own stay, as the guest app shows it: which hotel, which room, and when.
/// </summary>
/// <param name="Id">The stay.</param>
/// <param name="PropertyId">The hotel.</param>
/// <param name="PropertyName">The hotel's name, e.g. <c>Mysore Grand</c>.</param>
/// <param name="UnitNumber">The room, once assigned.</param>
/// <param name="Arrival">The arrival date.</param>
/// <param name="Departure">The departure date.</param>
public sealed record GuestStayResponse(
    StayContextId Id,
    PropertyId PropertyId,
    string PropertyName,
    string? UnitNumber,
    DateOnly Arrival,
    DateOnly Departure);
