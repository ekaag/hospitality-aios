using HospitalityOS.Application.Hours;
using HospitalityOS.Contracts.Api.Hours;
using HospitalityOS.Contracts.Api.Properties;
using HospitalityOS.SharedKernel.Collections;

namespace HospitalityOS.Seeding;

/// <summary>
/// Operating hours in seed data.
/// </summary>
internal static class SeedHours
{
    public static ValueList<WeeklyHours> Daily(params OpeningInterval[] intervals) =>
        ValueList.From(Enum.GetValues<DayOfWeek>().Select(day => new WeeklyHours(day, ValueList.From(intervals))));

    public static Task EnsureAsync(
        SeedSteps steps,
        IOperatingHoursAdministration hours,
        string record,
        Guid id,
        SubjectReference subject,
        ValueList<WeeklyHours> weekly,
        ValueList<HoursOverride> overrides,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(steps);
        ArgumentNullException.ThrowIfNull(hours);

        return steps.EnsureAsync(
            record,
            id,
            () => hours.GetAsync(id, cancellationToken),
            () => hours.CreateAsync(new CreateOperatingHoursRequest(AuroraGrand.PropertyId, subject, weekly, overrides), cancellationToken));
    }
}
