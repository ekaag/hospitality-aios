using HospitalityOS.SharedKernel.Domain;
using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Domain.Guests.StayContexts.Events;

/// <summary>
/// A stay was created (reserved).
/// </summary>
public sealed record StayContextCreated(
    StayContextId StayContextId,
    PropertyId PropertyId,
    GuestId GuestId,
    DateOnly Arrival,
    DateOnly Departure,
    AccommodationUnitId? AccommodationUnitId,
    StayStatus Status,
    DateTimeOffset OccurredAt) : IDomainEvent;
