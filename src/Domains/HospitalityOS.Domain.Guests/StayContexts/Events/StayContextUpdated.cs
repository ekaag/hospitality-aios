using HospitalityOS.SharedKernel.Domain;
using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Domain.Guests.StayContexts.Events;

/// <summary>
/// A stay's dates, unit, or status changed. Carries the full new state, not a diff.
/// </summary>
public sealed record StayContextUpdated(
    StayContextId StayContextId,
    DateOnly Arrival,
    DateOnly Departure,
    AccommodationUnitId? AccommodationUnitId,
    StayStatus Status,
    DateTimeOffset OccurredAt) : IDomainEvent;
