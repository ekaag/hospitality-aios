namespace HospitalityOS.Contracts.Api.Hours;

/// <summary>
/// Open from <paramref name="Opens"/> to <paramref name="Closes"/>, property-local. Closing at or before opening means
/// it ends the next day (<c>18:00</c> to <c>02:00</c>); <c>00:00</c> to <c>00:00</c> means all day.
/// </summary>
/// <param name="Opens">Opening time, e.g. <c>06:00:00</c>; required.</param>
/// <param name="Closes">Closing time; required.</param>
public sealed record OpeningInterval(
    TimeOnly? Opens = null,
    TimeOnly? Closes = null);
