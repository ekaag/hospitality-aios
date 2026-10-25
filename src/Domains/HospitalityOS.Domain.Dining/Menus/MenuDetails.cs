using HospitalityOS.SharedKernel.Collections;

namespace HospitalityOS.Domain.Dining.Menus;

/// <summary>
/// A menu's full content, edited as a unit. Validated and normalized by <see cref="Menu"/>.
/// </summary>
/// <param name="Name">Display name, e.g. <c>Dinner</c>; required.</param>
/// <param name="Items">Items in display order.</param>
/// <param name="Description">When or what the menu is, e.g. <c>Served 5:30-10pm</c>; optional.</param>
public sealed record MenuDetails(string Name, ValueList<MenuItem> Items, string? Description = null);
