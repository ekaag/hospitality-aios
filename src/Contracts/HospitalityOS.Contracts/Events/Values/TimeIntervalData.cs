namespace HospitalityOS.Contracts.Events.Values;

/// <summary>
/// An opening interval in property-local time; closing at or before opening means it ends the next day.
/// </summary>
/// <param name="Opens">Opening time.</param>
/// <param name="Closes">Closing time.</param>
public sealed record TimeIntervalData(
    TimeOnly Opens,
    TimeOnly Closes);
