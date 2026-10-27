namespace HospitalityOS.Contracts.Capabilities.Dining;

/// <summary>
/// A dietary property of a menu item.
/// </summary>
public enum DietaryTag
{
    /// <summary>
    /// Vegetarian.
    /// </summary>
    Vegetarian,

    /// <summary>
    /// Vegan.
    /// </summary>
    Vegan,

    /// <summary>
    /// Gluten-free.
    /// </summary>
    GlutenFree,

    /// <summary>
    /// Dairy-free.
    /// </summary>
    DairyFree,

    /// <summary>
    /// Nut-free.
    /// </summary>
    NutFree,

    /// <summary>
    /// Contains nuts.
    /// </summary>
    ContainsNuts,

    /// <summary>
    /// Contains shellfish.
    /// </summary>
    ContainsShellfish,

    /// <summary>
    /// Halal.
    /// </summary>
    Halal,

    /// <summary>
    /// Kosher.
    /// </summary>
    Kosher,

    /// <summary>
    /// Spicy.
    /// </summary>
    Spicy,
}
