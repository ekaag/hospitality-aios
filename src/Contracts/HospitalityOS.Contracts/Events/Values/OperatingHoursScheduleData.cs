using HospitalityOS.SharedKernel.Collections;

namespace HospitalityOS.Contracts.Events.Values;

/// <summary>
/// A full operating hours schedule.
/// </summary>
/// <param name="Weekly">Weekly hours.</param>
/// <param name="Overrides">Date overrides.</param>
/// <param name="Closures">Temporary closures.</param>
public sealed record OperatingHoursScheduleData(
    ValueList<DayHoursData> Weekly,
    ValueList<DateOverrideData> Overrides,
    ValueList<TemporaryClosureData> Closures);
