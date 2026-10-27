using HospitalityOS.SharedKernel.Collections;

namespace HospitalityOS.Contracts.Capabilities.Dining;

/// <summary>
/// Output of <c>Dining.GetMenu</c>.
/// </summary>
/// <param name="Restaurant">The restaurant record.</param>
/// <param name="RestaurantName">Its name.</param>
/// <param name="Menus">Its menus, by name; with a dietary filter, only menus with matching items.</param>
public sealed record GetMenuOutput(
    SourceReference Restaurant,
    string RestaurantName,
    ValueList<MenuEntry> Menus);
