using HospitalityOS.SharedKernel.Collections;

namespace HospitalityOS.Contracts.Events.Values;

/// <summary>
/// Weekly hours for one day.
/// </summary>
/// <param name="Day">Day of the week (<see cref="DayOfWeek"/> name).</param>
/// <param name="Intervals">Opening intervals; empty means closed.</param>
public sealed record DayHoursData(
    string Day,
    ValueList<TimeIntervalData> Intervals);
