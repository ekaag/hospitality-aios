using HospitalityOS.SharedKernel.Collections;

namespace HospitalityOS.Contracts.Events.Values;

/// <summary>
/// Hours that replace the weekly hours on one date.
/// </summary>
/// <param name="Date">The date (property-local).</param>
/// <param name="Intervals">Opening intervals; empty means closed.</param>
/// <param name="Note">Why, e.g. a holiday.</param>
public sealed record DateOverrideData(
    DateOnly Date,
    ValueList<TimeIntervalData> Intervals,
    string? Note);
