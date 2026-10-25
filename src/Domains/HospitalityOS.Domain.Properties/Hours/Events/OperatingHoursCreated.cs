using HospitalityOS.SharedKernel.Domain;
using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Domain.Properties.Hours.Events;

/// <summary>
/// Operating hours were created for a subject.
/// </summary>
public sealed record OperatingHoursCreated(
    OperatingHoursId OperatingHoursId,
    PropertyId PropertyId,
    HoursSubject Subject,
    OperatingHoursSchedule Schedule,
    DateTimeOffset OccurredAt) : IDomainEvent;
