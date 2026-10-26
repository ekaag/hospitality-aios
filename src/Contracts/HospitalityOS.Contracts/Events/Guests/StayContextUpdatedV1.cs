using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Contracts.Events.Guests;

/// <summary>
/// Version 1 of <c>StayContextUpdated</c>: a stay changed: check-in, room move, new dates, check-out, or cancellation.
/// </summary>
/// <param name="StayContextId">The stay.</param>
/// <param name="Arrival">Arrival date (property-local).</param>
/// <param name="Departure">Departure date (property-local).</param>
/// <param name="AccommodationUnitId">The accommodation unit, if any.</param>
/// <param name="Status">The status after the change (domain enum name).</param>
public sealed record StayContextUpdatedV1(
    StayContextId StayContextId,
    DateOnly Arrival,
    DateOnly Departure,
    AccommodationUnitId? AccommodationUnitId,
    string Status) : IEventPayload;
