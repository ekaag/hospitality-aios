namespace HospitalityOS.Domain.Services.Services;

/// <summary>
/// Which kind of team typically fulfils a <see cref="Service"/>; used by HumanAction routing (I3).
/// </summary>
public enum ServiceCategory
{
    /// <summary>
    /// Housekeeping, e.g. towels, pillows, cleaning.
    /// </summary>
    Housekeeping,

    /// <summary>
    /// Engineering, e.g. AC or plumbing issues.
    /// </summary>
    Maintenance,

    /// <summary>
    /// Food and beverage, e.g. room service.
    /// </summary>
    FoodAndBeverage,

    /// <summary>
    /// Concierge and front desk, e.g. bookings, wake-up calls.
    /// </summary>
    Concierge,

    /// <summary>
    /// Transport, e.g. airport shuttle, valet.
    /// </summary>
    Transport,

    /// <summary>
    /// Anything else.
    /// </summary>
    Other,
}
