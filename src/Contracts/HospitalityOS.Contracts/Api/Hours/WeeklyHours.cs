using HospitalityOS.SharedKernel.Collections;

namespace HospitalityOS.Contracts.Api.Hours;

/// <summary>
/// The usual hours of one day of the week. A day that is not listed, or has no intervals, is closed.
/// </summary>
/// <param name="Day">The day, e.g. <c>Monday</c>; required, at most once per schedule.</param>
/// <param name="Intervals">Opening intervals, which must not overlap.</param>
public sealed record WeeklyHours(
    DayOfWeek? Day = null,
    ValueList<OpeningInterval>? Intervals = null);
