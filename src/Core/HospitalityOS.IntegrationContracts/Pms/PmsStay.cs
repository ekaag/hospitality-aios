namespace HospitalityOS.IntegrationContracts.Pms;

/// <summary>
/// A stay as the PMS reports it, in canonical shape; the runtime turns it into a <c>StayContext</c>.
/// </summary>
/// <param name="Id">The provider's reservation ID.</param>
/// <param name="Guest">Who is staying.</param>
/// <param name="Arrival">Property-local arrival date.</param>
/// <param name="Departure">Property-local departure date.</param>
/// <param name="UnitNumber">The assigned unit as the property numbers it, e.g. <c>412</c>; <c>null</c> until assigned.</param>
/// <param name="Status">Where the stay is in its lifecycle.</param>
public sealed record PmsStay(
    ExternalId Id,
    PmsGuest Guest,
    DateOnly Arrival,
    DateOnly Departure,
    string? UnitNumber,
    StayStatus Status);
