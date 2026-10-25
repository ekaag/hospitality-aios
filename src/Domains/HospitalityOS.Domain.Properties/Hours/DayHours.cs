using HospitalityOS.SharedKernel.Collections;

namespace HospitalityOS.Domain.Properties.Hours;

/// <summary>
/// Regular hours for one day of the week. A day missing from the weekly schedule is closed.
/// </summary>
/// <param name="Day">The day of the week.</param>
/// <param name="Intervals">Opening intervals, non-overlapping; an interval may end the next day.</param>
public sealed record DayHours(DayOfWeek Day, ValueList<TimeInterval> Intervals);
