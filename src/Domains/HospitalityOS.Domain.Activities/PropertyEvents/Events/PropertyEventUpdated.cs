using HospitalityOS.SharedKernel.Domain;

namespace HospitalityOS.Domain.Activities.PropertyEvents.Events;

/// <summary>
/// A property event changed or was cancelled (spec §8). Carries the full new state, not a diff.
/// </summary>
public sealed record PropertyEventUpdated(
    PropertyEventId PropertyEventId,
    PropertyEventDetails Details,
    PropertyEventStatus Status,
    DateTimeOffset OccurredAt) : IDomainEvent;
