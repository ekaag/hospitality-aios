using HospitalityOS.Contracts.Capabilities.Dining;
using HospitalityOS.SharedKernel.Collections;
using HospitalityOS.SharedKernel.Pricing;

namespace HospitalityOS.Contracts.Api.Menus;

/// <summary>
/// A menu item, normalized (trimmed; dietary tags sorted, de-duplicated, vegan with vegetarian).
/// </summary>
/// <param name="Id">Stable ID; send it back to keep the item when replacing the menu.</param>
/// <param name="Name">Display name.</param>
/// <param name="Description">What it is.</param>
/// <param name="Price">Price, if published.</param>
/// <param name="Category">Section of the menu.</param>
/// <param name="Dietary">Dietary tags.</param>
/// <param name="IsAvailable">Whether it can currently be ordered.</param>
public sealed record MenuItemResponse(
    Guid Id,
    string Name,
    string? Description,
    Money? Price,
    string? Category,
    ValueList<DietaryTag> Dietary,
    bool IsAvailable);
