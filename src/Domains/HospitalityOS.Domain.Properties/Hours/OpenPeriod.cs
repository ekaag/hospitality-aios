namespace HospitalityOS.Domain.Properties.Hours;

/// <summary>
/// A concrete opening period in property-local time; <see cref="End"/> may fall on the next day.
/// </summary>
/// <param name="Start">Local start (inclusive), <see cref="DateTimeKind.Unspecified"/>.</param>
/// <param name="End">Local end (exclusive), <see cref="DateTimeKind.Unspecified"/>.</param>
public sealed record OpenPeriod(DateTime Start, DateTime End)
{
    /// <summary>
    /// Whether the local time falls in the period.
    /// </summary>
    public bool Contains(DateTime localTime) => localTime >= Start && localTime < End;
}
