using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Contracts.Events.Guests;

/// <summary>
/// Version 1 of <c>StayContextCreated</c>: a stay was created.
/// </summary>
/// <param name="StayContextId">The stay.</param>
/// <param name="PropertyId">The property.</param>
/// <param name="GuestId">The guest.</param>
/// <param name="Arrival">Arrival date (property-local).</param>
/// <param name="Departure">Departure date (property-local).</param>
/// <param name="AccommodationUnitId">The accommodation unit, if any.</param>
/// <param name="Status">The status after the change (domain enum name).</param>
public sealed record StayContextCreatedV1(
    StayContextId StayContextId,
    PropertyId PropertyId,
    GuestId GuestId,
    DateOnly Arrival,
    DateOnly Departure,
    AccommodationUnitId? AccommodationUnitId,
    string Status) : IEventPayload;
