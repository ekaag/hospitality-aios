using HospitalityOS.SharedKernel.Collections;
using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Contracts.Api.Menus;

/// <summary>
/// A menu with its items, normalized.
/// </summary>
/// <param name="Id">Platform-owned ID.</param>
/// <param name="PropertyId">The restaurant's property.</param>
/// <param name="RestaurantId">The restaurant serving it.</param>
/// <param name="Name">Display name.</param>
/// <param name="Description">When or what it is.</param>
/// <param name="Items">Items in display order.</param>
/// <param name="Version">Increases with every change; send it back in <c>If-Match</c> to update safely.</param>
public sealed record MenuResponse(
    Guid Id,
    PropertyId PropertyId,
    Guid RestaurantId,
    string Name,
    string? Description,
    ValueList<MenuItemResponse> Items,
    long Version);
