using HospitalityOS.SharedKernel.Collections;

namespace HospitalityOS.Contracts.Events.Values;

/// <summary>
/// A menu and its items.
/// </summary>
/// <param name="Name">Name.</param>
/// <param name="Items">Items.</param>
/// <param name="Description">Description.</param>
public sealed record MenuDetailsData(
    string Name,
    ValueList<MenuItemData> Items,
    string? Description);
