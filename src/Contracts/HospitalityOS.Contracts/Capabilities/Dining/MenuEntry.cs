using HospitalityOS.SharedKernel.Collections;

namespace HospitalityOS.Contracts.Capabilities.Dining;

/// <summary>
/// A menu.
/// </summary>
/// <param name="Source">The menu record.</param>
/// <param name="Name">Name.</param>
/// <param name="Description">Description.</param>
/// <param name="Items">Items, in menu order.</param>
public sealed record MenuEntry(
    SourceReference Source,
    string Name,
    string? Description,
    ValueList<MenuItemEntry> Items);
