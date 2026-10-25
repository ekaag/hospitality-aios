using HospitalityOS.Domain.Properties.Hours;
using HospitalityOS.Domain.Properties.Hours.Events;
using HospitalityOS.Domain.Properties.Shared;
using HospitalityOS.SharedKernel.Collections;
using HospitalityOS.SharedKernel.Results;
using HospitalityOS.UnitTests.Domain.Properties.TestDoubles;
using Xunit;

namespace HospitalityOS.UnitTests.Domain.Properties.Hours;

public sealed class OperatingHoursTests
{
    private static readonly OperatingHoursId id = new(Guid.Parse("01890a5d-ac96-774b-bcce-b30209904001"));
    private static readonly SubjectReference pool = new(SubjectKind.Space, Guid.Parse("01890a5d-ac96-774b-bcce-b30209904002"));
    private static readonly TimeZoneInfo losAngeles = TimeZoneInfo.FindSystemTimeZoneById("America/Los_Angeles");

    private static readonly DateOnly friday = new(2026, 10, 23);
    private static readonly DateOnly saturday = new(2026, 10, 24);
    private static readonly DateOnly sunday = new(2026, 10, 25);

    private static readonly ValueList<DayHours> everyDaySixToTen = ValueList.From(
        Enum.GetValues<DayOfWeek>().Select(day => new DayHours(day, [Interval("06:00", "22:00")])));

    [Fact]
    public void Create_WithWeeklyHours_CreatesAtVersionOneAndRaisesCreated()
    {
        OperatingHoursSchedule schedule = OperatingHoursSchedule.WeeklyOnly(everyDaySixToTen);

        OperatingHours hours = Create(schedule);

        Assert.Equal(pool, hours.Subject);
        Assert.Equal(1, hours.Version);
        Assert.Equal([new OperatingHoursCreated(id, AuroraGrand.PropertyId, pool, schedule, AuroraGrand.Now)], hours.DomainEvents);
    }

    [Fact]
    public void GetHoursFor_RegularDay_ReturnsLocalPeriodFromWeeklyHours()
    {
        OperatingHours hours = Create(OperatingHoursSchedule.WeeklyOnly(everyDaySixToTen));

        HoursForDate result = hours.GetHoursFor(saturday);

        Assert.Equal(HoursSource.Weekly, result.Source);
        Assert.Equal([Period(saturday, "06:00", saturday, "22:00")], result.Periods);
    }

    [Fact]
    public void GetHoursFor_DayMissingFromWeekly_IsClosed()
    {
        OperatingHours hours = Create(OperatingHoursSchedule.WeeklyOnly([new DayHours(DayOfWeek.Monday, [Interval("09:00", "17:00")])]));

        HoursForDate result = hours.GetHoursFor(saturday);

        Assert.True(result.IsClosed);
        Assert.Equal(HoursSource.Weekly, result.Source);
    }

    [Fact]
    public void GetHoursFor_DayWithSeveralIntervals_ReturnsThemInOrder()
    {
        OperatingHours hours = Create(OperatingHoursSchedule.WeeklyOnly(
            [new DayHours(DayOfWeek.Saturday, [Interval("17:30", "22:00"), Interval("07:00", "10:30")])]));

        HoursForDate result = hours.GetHoursFor(saturday);

        Assert.Equal([Period(saturday, "07:00", saturday, "10:30"), Period(saturday, "17:30", saturday, "22:00")], result.Periods);
    }

    [Fact]
    public void GetHoursFor_OverrideDate_UsesOverrideAndNote()
    {
        OperatingHours hours = Create(new OperatingHoursSchedule(
            everyDaySixToTen,
            [new DateOverride(saturday, [Interval("08:00", "18:00")], "Pool party setup")],
            []));

        HoursForDate result = hours.GetHoursFor(saturday);

        Assert.Equal(HoursSource.Override, result.Source);
        Assert.Equal("Pool party setup", result.Note);
        Assert.Equal([Period(saturday, "08:00", saturday, "18:00")], result.Periods);
    }

    [Fact]
    public void GetHoursFor_OverrideWithoutIntervals_IsClosedThatDate()
    {
        OperatingHours hours = Create(new OperatingHoursSchedule(everyDaySixToTen, [new DateOverride(saturday, [])], []));

        HoursForDate result = hours.GetHoursFor(saturday);

        Assert.True(result.IsClosed);
        Assert.Equal(HoursSource.Override, result.Source);
    }

    [Fact]
    public void GetHoursFor_TemporaryClosure_WinsOverOverrideAndWeekly()
    {
        OperatingHours hours = Create(new OperatingHoursSchedule(
            everyDaySixToTen,
            [new DateOverride(saturday, [Interval("08:00", "18:00")])],
            [new TemporaryClosure(friday, saturday, "Pool resurfacing")]));

        Assert.Equal(new HoursForDate(friday, HoursSource.Closure, [], "Pool resurfacing"), hours.GetHoursFor(friday));
        Assert.Equal(new HoursForDate(saturday, HoursSource.Closure, [], "Pool resurfacing"), hours.GetHoursFor(saturday));
        Assert.Equal(HoursSource.Weekly, hours.GetHoursFor(sunday).Source);
    }

    [Fact]
    public void GetHoursFor_OvernightInterval_EndsNextDay()
    {
        OperatingHours hours = Create(OperatingHoursSchedule.WeeklyOnly([new DayHours(DayOfWeek.Friday, [Interval("18:00", "02:00")])]));

        HoursForDate result = hours.GetHoursFor(friday);

        Assert.Equal([Period(friday, "18:00", saturday, "02:00")], result.Periods);
    }

    [Fact]
    public void GetHoursFor_AllDayInterval_SpansWholeDay()
    {
        OperatingHours hours = Create(OperatingHoursSchedule.WeeklyOnly([new DayHours(DayOfWeek.Saturday, [TimeInterval.AllDay])]));

        HoursForDate result = hours.GetHoursFor(saturday);

        Assert.Equal([Period(saturday, "00:00", sunday, "00:00")], result.Periods);
    }

    [Theory]
    [InlineData("2026-10-24T04:30:00Z", true)]   // 21:30 Friday in Los Angeles (PDT, UTC-7)
    [InlineData("2026-10-24T05:30:00Z", false)]  // 22:30 Friday in Los Angeles: pool closed at 22:00
    [InlineData("2026-10-24T13:00:00Z", true)]   // 06:00 Saturday in Los Angeles: opening is inclusive
    public void GetStatusAt_ConvertsInstantToPropertyLocalTime(string instant, bool expectedOpen)
    {
        OperatingHours hours = Create(OperatingHoursSchedule.WeeklyOnly(everyDaySixToTen));

        OpenStatus status = hours.GetStatusAt(DateTimeOffset.Parse(instant, System.Globalization.CultureInfo.InvariantCulture), losAngeles);

        Assert.Equal(expectedOpen, status.IsOpen);
    }

    [Fact]
    public void GetStatusAt_AfterMidnight_FindsPreviousEveningsOvernightPeriod()
    {
        OperatingHours bar = Create(OperatingHoursSchedule.WeeklyOnly([new DayHours(DayOfWeek.Friday, [Interval("18:00", "02:00")])]));
        DateTimeOffset saturdayOneAm = new(2026, 10, 24, 1, 0, 0, TimeSpan.FromHours(-7));

        OpenStatus status = bar.GetStatusAt(saturdayOneAm, losAngeles);

        Assert.True(status.IsOpen);
        Assert.Equal(Period(friday, "18:00", saturday, "02:00"), status.CurrentPeriod);
        Assert.Equal(saturday.ToDateTime(new TimeOnly(1, 0)), status.LocalTime);
    }

    [Fact]
    public void Create_WithUnsortedInput_NormalizesSchedule()
    {
        OperatingHoursSchedule messy = new(
            [new DayHours(DayOfWeek.Saturday, [Interval("17:00", "21:00"), Interval("07:00", "11:00")]), new DayHours(DayOfWeek.Monday, [Interval("09:00", "17:00")])],
            [new DateOverride(sunday, []), new DateOverride(saturday, [], "  ")],
            [new TemporaryClosure(sunday, sunday, " Repairs "), new TemporaryClosure(friday, friday)]);

        OperatingHours hours = Create(messy);

        Assert.Equal(
            new OperatingHoursSchedule(
                [new DayHours(DayOfWeek.Monday, [Interval("09:00", "17:00")]), new DayHours(DayOfWeek.Saturday, [Interval("07:00", "11:00"), Interval("17:00", "21:00")])],
                [new DateOverride(saturday, []), new DateOverride(sunday, [])],
                [new TemporaryClosure(friday, friday), new TemporaryClosure(sunday, sunday, "Repairs")]),
            hours.Schedule);
    }

    [Fact]
    public void Create_WithInvalidSchedule_ReportsEveryProblem()
    {
        OperatingHoursSchedule invalid = new(
            [
                new DayHours(DayOfWeek.Monday, [Interval("10:00", "10:00")]),
                new DayHours(DayOfWeek.Tuesday, [Interval("09:00", "13:00"), Interval("12:00", "17:00")]),
                new DayHours(DayOfWeek.Monday, [Interval("09:00", "17:00")]),
            ],
            [new DateOverride(saturday, []), new DateOverride(saturday, [Interval("09:00", "12:00")])],
            [new TemporaryClosure(sunday, friday)]);

        Result<OperatingHours> result = OperatingHours.Create(id, AuroraGrand.TenantId, AuroraGrand.PropertyId, new SubjectReference(SubjectKind.Space, Guid.Empty), invalid, AuroraGrand.Now);

        Assert.Equal(
            [
                ("subject.id", "hours.subject_id_required"),
                ("weekly[0].intervals[0]", "hours.interval_invalid"),
                ("weekly[1].intervals", "hours.intervals_overlap"),
                ("weekly[2].day", "hours.day_duplicate"),
                ("overrides[1].date", "hours.override_date_duplicate"),
                ("closures[0]", "hours.closure_range_invalid"),
            ],
            FieldCodes(result));
    }

    [Fact]
    public void Create_WithOvernightHoursOverlappingNextDay_ReturnsValidationError()
    {
        OperatingHoursSchedule schedule = OperatingHoursSchedule.WeeklyOnly(
            [new DayHours(DayOfWeek.Saturday, [Interval("18:00", "02:00")]), new DayHours(DayOfWeek.Sunday, [Interval("01:00", "05:00")])]);

        Result<OperatingHours> result = OperatingHours.Create(id, AuroraGrand.TenantId, AuroraGrand.PropertyId, pool, schedule, AuroraGrand.Now);

        Assert.Equal([("weekly[0].intervals", "hours.overnight_overlaps_next_day")], FieldCodes(result));
    }

    [Fact]
    public void Create_WithTooManyIntervalsOrAllDayPlusOthers_ReturnsValidationErrors()
    {
        ValueList<TimeInterval> seven = ValueList.From(Enumerable.Range(0, 7).Select(hour => Interval($"{hour:D2}:00", $"{hour:D2}:30")));
        OperatingHoursSchedule schedule = OperatingHoursSchedule.WeeklyOnly(
            [new DayHours(DayOfWeek.Monday, seven), new DayHours(DayOfWeek.Tuesday, [TimeInterval.AllDay, Interval("09:00", "10:00")])]);

        Result<OperatingHours> result = OperatingHours.Create(id, AuroraGrand.TenantId, AuroraGrand.PropertyId, pool, schedule, AuroraGrand.Now);

        Assert.Equal(
            [("weekly[0].intervals", "hours.too_many_intervals"), ("weekly[1].intervals", "hours.intervals_overlap")],
            FieldCodes(result));
    }

    [Fact]
    public void Update_WithChangedSchedule_ReplacesBumpsVersionAndRaisesUpdated()
    {
        OperatingHours hours = Create(OperatingHoursSchedule.WeeklyOnly(everyDaySixToTen));
        hours.DequeueDomainEvents();
        OperatingHoursSchedule winterHours = new(everyDaySixToTen, [], [new TemporaryClosure(sunday, sunday, "Maintenance")]);

        Result result = hours.Update(winterHours, AuroraGrand.Now.AddHours(1));

        Assert.True(result.IsSuccess);
        Assert.Equal(2, hours.Version);
        Assert.Equal([new OperatingHoursUpdated(id, winterHours, AuroraGrand.Now.AddHours(1))], hours.DomainEvents);
        Assert.Equal(HoursSource.Closure, hours.GetHoursFor(sunday).Source);
    }

    [Fact]
    public void Update_WithEquivalentScheduleInDifferentOrder_IsNoOpAndKeepsVersion()
    {
        OperatingHours hours = Create(OperatingHoursSchedule.WeeklyOnly(everyDaySixToTen));
        hours.DequeueDomainEvents();

        Result result = hours.Update(OperatingHoursSchedule.WeeklyOnly(ValueList.From(everyDaySixToTen.Reverse())), AuroraGrand.Now.AddHours(1));

        Assert.True(result.IsSuccess);
        Assert.Equal(1, hours.Version);
        Assert.Empty(hours.DomainEvents);
    }

    [Fact]
    public void Update_WithInvalidSchedule_KeepsScheduleAndVersion()
    {
        OperatingHours hours = Create(OperatingHoursSchedule.WeeklyOnly(everyDaySixToTen));
        hours.DequeueDomainEvents();

        Result result = hours.Update(OperatingHoursSchedule.WeeklyOnly([new DayHours(DayOfWeek.Monday, [Interval("10:00", "10:00")])]), AuroraGrand.Now);

        Assert.True(result.IsFailure);
        Assert.Equal(1, hours.Version);
        Assert.Equal(OperatingHoursSchedule.WeeklyOnly(everyDaySixToTen), hours.Schedule);
    }

    private static OperatingHours Create(OperatingHoursSchedule schedule) =>
        OperatingHours.Create(id, AuroraGrand.TenantId, AuroraGrand.PropertyId, pool, schedule, AuroraGrand.Now).Value;

    private static TimeInterval Interval(string opens, string closes) =>
        new(TimeOnly.Parse(opens, System.Globalization.CultureInfo.InvariantCulture), TimeOnly.Parse(closes, System.Globalization.CultureInfo.InvariantCulture));

    private static OpenPeriod Period(DateOnly startDate, string start, DateOnly endDate, string end) =>
        new(
            startDate.ToDateTime(TimeOnly.Parse(start, System.Globalization.CultureInfo.InvariantCulture)),
            endDate.ToDateTime(TimeOnly.Parse(end, System.Globalization.CultureInfo.InvariantCulture)));

    private static List<(string Field, string Code)> FieldCodes(Result result)
    {
        ValidationError error = Assert.IsType<ValidationError>(result.Error);

        return [.. error.Fields.Select(field => (field.Field, field.Code))];
    }
}
