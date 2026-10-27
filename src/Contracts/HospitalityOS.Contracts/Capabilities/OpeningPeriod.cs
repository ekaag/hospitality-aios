namespace HospitalityOS.Contracts.Capabilities;

/// <summary>
/// One period of opening, in property-local time; it may end the next day.
/// </summary>
/// <param name="Start">Opens (property-local).</param>
/// <param name="End">Closes (property-local).</param>
public sealed record OpeningPeriod(
    DateTime Start,
    DateTime End);
