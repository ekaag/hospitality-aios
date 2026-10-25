namespace HospitalityOS.Domain.Properties.Hours;

/// <summary>
/// One opening interval in property-local time.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item><see cref="Closes"/> after <see cref="Opens"/>: same-day interval, e.g. 06:00–22:00.</item>
/// <item><see cref="Closes"/> before <see cref="Opens"/>: overnight, closing the next day, e.g. 18:00–02:00.</item>
/// <item>00:00–00:00: open all day. Any other equal pair is invalid.</item>
/// </list>
/// </remarks>
/// <param name="Opens">Local opening time.</param>
/// <param name="Closes">Local closing time.</param>
public sealed record TimeInterval(TimeOnly Opens, TimeOnly Closes)
{
    internal const int MinutesPerDay = 24 * 60;

    /// <summary>
    /// Open all day (00:00–00:00).
    /// </summary>
    public static TimeInterval AllDay { get; } = new(TimeOnly.MinValue, TimeOnly.MinValue);

    /// <summary>
    /// Whether the interval runs past midnight into the next day (true for <see cref="AllDay"/>).
    /// </summary>
    public bool EndsNextDay => Closes <= Opens;

    /// <summary>
    /// Whether the times describe a usable interval.
    /// </summary>
    public bool IsValid => Opens != Closes || this == AllDay;

    /// <inheritdoc />
    public override string ToString() => $"{Opens:HH\\:mm}-{Closes:HH\\:mm}";

    /// <summary>
    /// The concrete local period when the interval opens on <paramref name="date"/>.
    /// </summary>
    internal OpenPeriod On(DateOnly date) =>
        new(date.ToDateTime(Opens), (EndsNextDay ? date.AddDays(1) : date).ToDateTime(Closes));

    /// <summary>
    /// Start and end as minutes from midnight of the opening day; the end exceeds 1440 for overnight intervals.
    /// </summary>
    internal (int Start, int End) ToMinutes()
    {
        int start = (Opens.Hour * 60) + Opens.Minute;
        int end = (Closes.Hour * 60) + Closes.Minute;

        return (start, EndsNextDay ? end + MinutesPerDay : end);
    }
}
