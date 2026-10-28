namespace HospitalityOS.Contracts.Api.Services;

/// <summary>
/// Which kind of team fulfils a service.
/// </summary>
public enum ServiceCategory
{
    /// <summary>
    /// Housekeeping, e.g. towels and turndown.
    /// </summary>
    Housekeeping,

    /// <summary>
    /// Maintenance, e.g. a broken air conditioner.
    /// </summary>
    Maintenance,

    /// <summary>
    /// Food and beverage, e.g. room service.
    /// </summary>
    FoodAndBeverage,

    /// <summary>
    /// Concierge, e.g. recommendations and arrangements.
    /// </summary>
    Concierge,

    /// <summary>
    /// Transport, e.g. airport transfers.
    /// </summary>
    Transport,

    /// <summary>
    /// Anything else.
    /// </summary>
    Other,
}
