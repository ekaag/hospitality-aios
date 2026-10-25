namespace HospitalityOS.Domain.Activities.PropertyEvents;

/// <summary>
/// Broad grouping of a <see cref="PropertyEvent"/>, used to filter discovery ("anything for kids tonight?").
/// </summary>
public enum PropertyEventCategory
{
    /// <summary>
    /// Live music, shows, movie nights.
    /// </summary>
    Entertainment,

    /// <summary>
    /// Tastings, special dinners, happy hours.
    /// </summary>
    FoodAndDrink,

    /// <summary>
    /// Classes such as yoga or spin.
    /// </summary>
    Fitness,

    /// <summary>
    /// Spa and mindfulness sessions.
    /// </summary>
    Wellness,

    /// <summary>
    /// Kids' club and family activities.
    /// </summary>
    Family,

    /// <summary>
    /// Tours and excursions.
    /// </summary>
    Tour,

    /// <summary>
    /// Mixers and receptions.
    /// </summary>
    Social,

    /// <summary>
    /// Anything else.
    /// </summary>
    Other,
}
