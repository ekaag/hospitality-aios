using HospitalityOS.Contracts.Api.Properties;
using HospitalityOS.SharedKernel.Collections;
using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Contracts.Api.Hours;

/// <summary>
/// An operating hours record, normalized: days in week order, intervals by opening time, overrides by date, closures
/// by start.
/// </summary>
/// <param name="Id">Platform-owned ID.</param>
/// <param name="PropertyId">The property.</param>
/// <param name="Subject">Whose hours.</param>
/// <param name="Weekly">The usual hours per day of the week.</param>
/// <param name="Overrides">Dates with different hours.</param>
/// <param name="Closures">Date ranges when it is closed.</param>
/// <param name="Version">Increases with every change; send it back in <c>If-Match</c> to update safely.</param>
public sealed record OperatingHoursResponse(
    Guid Id,
    PropertyId PropertyId,
    SubjectReference Subject,
    ValueList<WeeklyHours> Weekly,
    ValueList<HoursOverride> Overrides,
    ValueList<HoursClosure> Closures,
    long Version);
