namespace HospitalityOS.Contracts.Api.Hours;

/// <summary>
/// Closed on every date from <paramref name="From"/> to <paramref name="To"/>, whatever the weekly hours or overrides
/// say, e.g. for maintenance.
/// </summary>
/// <param name="From">First closed property-local date; required.</param>
/// <param name="To">Last closed date, on or after <paramref name="From"/>; required.</param>
/// <param name="Reason">Why, e.g. <c>Pool maintenance</c>.</param>
public sealed record HoursClosure(
    DateOnly? From = null,
    DateOnly? To = null,
    string? Reason = null);
