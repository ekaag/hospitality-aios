using HospitalityOS.Contracts.Events.Values;

namespace HospitalityOS.Contracts.Events.Properties;

/// <summary>
/// Version 1 of <c>OperatingHoursUpdated</c>: operating hours changed (Journey 16).
/// </summary>
/// <param name="OperatingHoursId">The operating hours record.</param>
/// <param name="Schedule">The full schedule after the change.</param>
public sealed record OperatingHoursUpdatedV1(
    Guid OperatingHoursId,
    OperatingHoursScheduleData Schedule) : IEventPayload;
