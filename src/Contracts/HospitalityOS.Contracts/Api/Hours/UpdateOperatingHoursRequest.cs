using HospitalityOS.SharedKernel.Collections;

namespace HospitalityOS.Contracts.Api.Hours;

/// <summary>
/// <c>PUT /api/v1/operating-hours/{id}</c>: replaces the whole schedule (Journey 16). The subject does not change.
/// </summary>
/// <param name="Weekly">The usual hours per day of the week.</param>
/// <param name="Overrides">Dates with different hours.</param>
/// <param name="Closures">Date ranges when it is closed.</param>
public sealed record UpdateOperatingHoursRequest(
    ValueList<WeeklyHours>? Weekly = null,
    ValueList<HoursOverride>? Overrides = null,
    ValueList<HoursClosure>? Closures = null);
