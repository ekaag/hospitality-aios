using HospitalityOS.Contracts.Capabilities;
using HospitalityOS.Domain.Properties.Hours;
using HospitalityOS.SharedKernel.Collections;

namespace HospitalityOS.Capabilities.Reading;

/// <summary>
/// An hours record's answer for one date, cited, with whether the subject is open right now.
/// </summary>
internal static class Hours
{
    public static HoursOnDate On(OperatingHours hours, DateOnly date, PropertyClock clock)
    {
        HoursForDate day = hours.GetHoursFor(date);

        return new HoursOnDate(
            Sources.Of(hours),
            day.Date,
            ContractEnum.To<HoursBasis>(day.Source),
            ValueList.From(day.Periods.Select(period => new OpeningPeriod(period.Start, period.End))),
            day.Note,
            hours.GetStatusAt(clock.Now, clock.TimeZone).IsOpen);
    }
}
