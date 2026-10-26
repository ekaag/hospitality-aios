using HospitalityOS.Contracts.Events.Values;
using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Contracts.Events.Properties;

/// <summary>
/// Version 1 of <c>OperatingHoursCreated</c>: operating hours were set for a subject (Journey 2).
/// </summary>
/// <param name="OperatingHoursId">The operating hours record.</param>
/// <param name="PropertyId">The property.</param>
/// <param name="Subject">What the hours are for.</param>
/// <param name="Schedule">The full schedule after the change.</param>
public sealed record OperatingHoursCreatedV1(
    Guid OperatingHoursId,
    PropertyId PropertyId,
    SubjectReferenceData Subject,
    OperatingHoursScheduleData Schedule) : IEventPayload;
