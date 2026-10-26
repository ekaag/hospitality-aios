using HospitalityOS.SharedKernel.Collections;
using HospitalityOS.SharedKernel.Pricing;

namespace HospitalityOS.Contracts.Events.Values;

/// <summary>
/// One menu item.
/// </summary>
/// <param name="Id">Stable item ID, cited by answers.</param>
/// <param name="Name">Name.</param>
/// <param name="Description">Description.</param>
/// <param name="Price">Price; <c>null</c> when not published.</param>
/// <param name="Category">Menu section.</param>
/// <param name="Dietary">Dietary tags (domain enum names); absent means not stated, never safe.</param>
/// <param name="IsAvailable">Whether it can be ordered now.</param>
public sealed record MenuItemData(
    Guid Id,
    string Name,
    string? Description,
    Money? Price,
    string? Category,
    ValueList<string> Dietary,
    bool IsAvailable);
