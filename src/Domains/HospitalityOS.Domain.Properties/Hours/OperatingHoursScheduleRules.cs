using HospitalityOS.SharedKernel.Collections;
using HospitalityOS.SharedKernel.Results;
using HospitalityOS.SharedKernel.Validation;

namespace HospitalityOS.Domain.Properties.Hours;

/// <summary>
/// Validates and normalizes an <see cref="OperatingHoursSchedule"/> (sorted days, intervals, overrides, and
/// closures; trimmed notes), reporting every problem at once. Field paths follow the request shape,
/// e.g. <c>weekly[2].intervals[0]</c>.
/// </summary>
internal static class OperatingHoursScheduleRules
{
    public const int MaxIntervalsPerDay = 6;

    public static Result<OperatingHoursSchedule> Validate(OperatingHoursSchedule schedule)
    {
        List<FieldError> errors = [];

        ValueList<DayHours> weekly = ValidateWeekly(schedule.Weekly ?? [], errors);
        ValueList<DateOverride> overrides = ValidateOverrides(schedule.Overrides ?? [], errors);
        ValueList<TemporaryClosure> closures = ValidateClosures(schedule.Closures ?? [], errors);

        return errors.Count > 0
            ? ValidationError.From(errors)
            : new OperatingHoursSchedule(weekly, overrides, closures);
    }

    private static ValueList<DayHours> ValidateWeekly(ValueList<DayHours> weekly, List<FieldError> errors)
    {
        HashSet<DayOfWeek> seen = [];
        List<DayHours> normalized = [];
        int errorCount = errors.Count;

        for (int i = 0; i < weekly.Count; i++)
        {
            string path = $"weekly[{i}]";
            DayHours? day = weekly[i];

            if (day is null)
            {
                errors.Add(new FieldError(path, "hours.day_required", $"{path} is required."));
                continue;
            }

            if (!Enum.IsDefined(day.Day))
            {
                errors.Add(new FieldError(path + ".day", "hours.day_invalid", $"'{day.Day}' is not a day of the week."));
                continue;
            }

            if (!seen.Add(day.Day))
            {
                errors.Add(new FieldError(path + ".day", "hours.day_duplicate", $"{day.Day} appears more than once."));
                continue;
            }

            normalized.Add(day with { Intervals = ValidateIntervals(day.Intervals ?? [], path + ".intervals", errors) });
        }

        normalized.Sort((left, right) => left.Day.CompareTo(right.Day));

        // Spillover is only meaningful once every day's own intervals are valid; otherwise an invalid
        // interval such as 10:00-10:00 would also be reported as overnight.
        if (errors.Count == errorCount)
        {
            CheckOvernightSpillover(normalized, weekly, errors);
        }

        return ValueList.From(normalized);
    }

    // An overnight interval must close before the next day's first interval opens, e.g. Fri 18:00–02:00
    // conflicts with Sat 01:00–05:00. Saturday wraps around to Sunday.
    private static void CheckOvernightSpillover(List<DayHours> days, ValueList<DayHours> original, List<FieldError> errors)
    {
        Dictionary<DayOfWeek, DayHours> byDay = days.ToDictionary(day => day.Day);

        foreach (DayHours day in days)
        {
            int spillEnd = day.Intervals.Select(interval => interval.ToMinutes().End - TimeInterval.MinutesPerDay).DefaultIfEmpty(0).Max();
            DayOfWeek next = (DayOfWeek)(((int)day.Day + 1) % 7);

            if (spillEnd > 0
                && byDay.TryGetValue(next, out DayHours? nextDay)
                && nextDay.Intervals.Any(interval => interval.ToMinutes().Start < spillEnd))
            {
                int index = original.Select((entry, position) => (entry, position)).First(pair => pair.entry?.Day == day.Day).position;
                errors.Add(new FieldError(
                    $"weekly[{index}].intervals",
                    "hours.overnight_overlaps_next_day",
                    $"{day.Day}'s overnight hours overlap {next}'s opening hours."));
            }
        }
    }

    private static ValueList<DateOverride> ValidateOverrides(ValueList<DateOverride> overrides, List<FieldError> errors)
    {
        HashSet<DateOnly> seen = [];
        List<DateOverride> normalized = [];

        for (int i = 0; i < overrides.Count; i++)
        {
            string path = $"overrides[{i}]";
            DateOverride? entry = overrides[i];

            if (entry is null)
            {
                errors.Add(new FieldError(path, "hours.override_required", $"{path} is required."));
                continue;
            }

            if (!seen.Add(entry.Date))
            {
                errors.Add(new FieldError(path + ".date", "hours.override_date_duplicate", $"{entry.Date:yyyy-MM-dd} has more than one override."));
                continue;
            }

            normalized.Add(new DateOverride(
                entry.Date,
                ValidateIntervals(entry.Intervals ?? [], path + ".intervals", errors),
                TextRules.Optional(entry.Note, path + ".note", "hours.override_note", errors)));
        }

        return ValueList.From(normalized.OrderBy(entry => entry.Date));
    }

    private static ValueList<TemporaryClosure> ValidateClosures(ValueList<TemporaryClosure> closures, List<FieldError> errors)
    {
        List<TemporaryClosure> normalized = [];

        for (int i = 0; i < closures.Count; i++)
        {
            string path = $"closures[{i}]";
            TemporaryClosure? closure = closures[i];

            if (closure is null)
            {
                errors.Add(new FieldError(path, "hours.closure_required", $"{path} is required."));
                continue;
            }

            if (closure.To < closure.From)
            {
                errors.Add(new FieldError(path, "hours.closure_range_invalid", "A closure must end on or after the day it starts."));
            }

            normalized.Add(closure with { Reason = TextRules.Optional(closure.Reason, path + ".reason", "hours.closure_reason", errors) });
        }

        return ValueList.From(normalized.OrderBy(closure => closure.From).ThenBy(closure => closure.To));
    }

    private static ValueList<TimeInterval> ValidateIntervals(ValueList<TimeInterval> intervals, string path, List<FieldError> errors)
    {
        int errorCount = errors.Count;

        if (intervals.Count > MaxIntervalsPerDay)
        {
            errors.Add(new FieldError(path, "hours.too_many_intervals", $"At most {MaxIntervalsPerDay} intervals are allowed per day."));
        }

        for (int i = 0; i < intervals.Count; i++)
        {
            if (intervals[i] is not { IsValid: true })
            {
                errors.Add(new FieldError($"{path}[{i}]", "hours.interval_invalid", "An interval needs different opening and closing times (00:00-00:00 means all day)."));
            }
        }

        if (errors.Count > errorCount)
        {
            return intervals;
        }

        List<TimeInterval> sorted = [.. intervals.OrderBy(interval => interval.Opens)];

        if (sorted.Contains(TimeInterval.AllDay) && sorted.Count > 1)
        {
            errors.Add(new FieldError(path, "hours.intervals_overlap", "An all-day interval cannot be combined with other intervals."));
        }
        else if (sorted.Zip(sorted.Skip(1)).Any(pair => pair.First.ToMinutes().End > pair.Second.ToMinutes().Start))
        {
            errors.Add(new FieldError(path, "hours.intervals_overlap", "Intervals on the same day must not overlap."));
        }

        return ValueList.From(sorted);
    }
}
