namespace HospitalityOS.Domain.Properties.Hours;

/// <summary>
/// Whether a subject is open at an instant.
/// </summary>
/// <param name="LocalTime">The instant in property-local time.</param>
/// <param name="CurrentPeriod">The period that is open now, or <c>null</c> when closed.</param>
public sealed record OpenStatus(DateTime LocalTime, OpenPeriod? CurrentPeriod)
{
    /// <summary>
    /// Whether the subject is open.
    /// </summary>
    public bool IsOpen => CurrentPeriod is not null;
}
