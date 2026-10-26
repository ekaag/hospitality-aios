namespace HospitalityOS.Contracts.Events.Values;

/// <summary>
/// A closure over a date range, overriding everything else.
/// </summary>
/// <param name="From">First closed date (property-local).</param>
/// <param name="To">Last closed date (property-local).</param>
/// <param name="Reason">Why.</param>
public sealed record TemporaryClosureData(
    DateOnly From,
    DateOnly To,
    string? Reason);
