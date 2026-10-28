using HospitalityOS.SharedKernel.Collections;

namespace HospitalityOS.Contracts.Api.Hours;

/// <summary>
/// Hours that replace the weekly hours on one date, e.g. a holiday.
/// </summary>
/// <param name="Date">The property-local date; required, at most once per schedule.</param>
/// <param name="Intervals">Opening intervals; none means closed that day.</param>
/// <param name="Note">Why, e.g. <c>Bank holiday</c>.</param>
public sealed record HoursOverride(
    DateOnly? Date = null,
    ValueList<OpeningInterval>? Intervals = null,
    string? Note = null);
