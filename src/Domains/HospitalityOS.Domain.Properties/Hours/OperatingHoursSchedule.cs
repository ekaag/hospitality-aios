using HospitalityOS.SharedKernel.Collections;

namespace HospitalityOS.Domain.Properties.Hours;

/// <summary>
/// A complete set of opening hours in property-local time. For any date, the first matching rule wins:
/// a <see cref="TemporaryClosure"/>, then a <see cref="DateOverride"/>, then the <see cref="Weekly"/> hours.
/// </summary>
/// <param name="Weekly">Regular hours per day of the week; at most one entry per day, missing days are closed.</param>
/// <param name="Overrides">Date-specific hours; at most one per date.</param>
/// <param name="Closures">Temporary closures.</param>
public sealed record OperatingHoursSchedule(
    ValueList<DayHours> Weekly,
    ValueList<DateOverride> Overrides,
    ValueList<TemporaryClosure> Closures)
{
    /// <summary>
    /// A schedule with only regular weekly hours.
    /// </summary>
    public static OperatingHoursSchedule WeeklyOnly(ValueList<DayHours> weekly) => new(weekly, [], []);
}
