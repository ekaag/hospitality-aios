using HospitalityOS.SharedKernel.Collections;

namespace HospitalityOS.Domain.Properties.Hours;

/// <summary>
/// Replaces the regular hours on one local date, e.g. a holiday. No intervals means closed that day.
/// </summary>
/// <param name="Date">The property-local date.</param>
/// <param name="Intervals">Opening intervals for that date; empty when closed.</param>
/// <param name="Note">Why the hours differ, e.g. <c>New Year's Day</c>; optional.</param>
public sealed record DateOverride(DateOnly Date, ValueList<TimeInterval> Intervals, string? Note = null);
