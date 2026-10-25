using HospitalityOS.SharedKernel.Domain;

namespace HospitalityOS.Domain.Properties.Hours.Events;

/// <summary>
/// A subject's operating hours changed (Journey 16). Carries the full new schedule, not a diff.
/// </summary>
public sealed record OperatingHoursUpdated(
    OperatingHoursId OperatingHoursId,
    OperatingHoursSchedule Schedule,
    DateTimeOffset OccurredAt) : IDomainEvent;
