using HospitalityOS.SharedKernel.Domain;
using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Domain.Activities.PropertyEvents.Events;

/// <summary>
/// A property event was scheduled (spec §8).
/// </summary>
public sealed record PropertyEventCreated(
    PropertyEventId PropertyEventId,
    PropertyId PropertyId,
    PropertyEventDetails Details,
    PropertyEventStatus Status,
    DateTimeOffset OccurredAt) : IDomainEvent;
