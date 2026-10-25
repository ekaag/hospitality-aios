namespace HospitalityOS.Domain.Properties.Hours;

/// <summary>
/// Closed for a range of local dates, e.g. maintenance. Takes precedence over overrides and regular hours.
/// </summary>
/// <param name="From">First closed date (property-local, inclusive).</param>
/// <param name="To">Last closed date (property-local, inclusive).</param>
/// <param name="Reason">Why it is closed, e.g. <c>Pool resurfacing</c>; optional but shown to guests.</param>
public sealed record TemporaryClosure(DateOnly From, DateOnly To, string? Reason = null)
{
    /// <summary>
    /// Whether <paramref name="date"/> falls in the closure.
    /// </summary>
    public bool Covers(DateOnly date) => date >= From && date <= To;
}
