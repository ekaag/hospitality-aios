namespace HospitalityOS.Domain.Dining.Menus;

/// <summary>
/// Dietary tags guests filter on ("what vegetarian food can I get?", Journey 7). Only tags the
/// property has recorded are claimed; absence means "not stated", not "safe".
/// </summary>
public enum DietaryTag
{
    /// <summary>
    /// No meat or fish.
    /// </summary>
    Vegetarian,

    /// <summary>
    /// No animal products; implies <see cref="Vegetarian"/>.
    /// </summary>
    Vegan,

    /// <summary>
    /// Prepared without gluten.
    /// </summary>
    GlutenFree,

    /// <summary>
    /// Prepared without dairy.
    /// </summary>
    DairyFree,

    /// <summary>
    /// Prepared without nuts; contradicts <see cref="ContainsNuts"/>.
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
