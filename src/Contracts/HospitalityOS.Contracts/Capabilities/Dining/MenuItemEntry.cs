using HospitalityOS.SharedKernel.Collections;
using HospitalityOS.SharedKernel.Pricing;

namespace HospitalityOS.Contracts.Capabilities.Dining;

/// <summary>
/// A menu item.
/// </summary>
/// <param name="ItemId">The item.</param>
/// <param name="Name">Name.</param>
/// <param name="Description">Description.</param>
/// <param name="Price">Price; absent when not listed (never guess one).</param>
/// <param name="Category">Section, e.g. <c>Starters</c>.</param>
/// <param name="Dietary">Dietary tags.</param>
/// <param name="IsAvailable">Whether it can be ordered now.</param>
public sealed record MenuItemEntry(
    Guid ItemId,
    string Name,
    string? Description,
    Money? Price,
    string? Category,
    ValueList<DietaryTag> Dietary,
    bool IsAvailable);
