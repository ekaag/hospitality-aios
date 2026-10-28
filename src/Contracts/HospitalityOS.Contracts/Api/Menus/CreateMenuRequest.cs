using HospitalityOS.SharedKernel.Collections;

namespace HospitalityOS.Contracts.Api.Menus;

/// <summary>
/// <c>POST /api/v1/menus</c>: adds a menu to a restaurant (Journeys 3 and 7). The menu belongs to the restaurant's
/// property.
/// Every field may be omitted: missing required fields are reported by validation, field by field.
/// </summary>
/// <param name="RestaurantId">The restaurant serving it; required.</param>
/// <param name="Name">Display name, e.g. <c>Dinner</c>; required.</param>
/// <param name="Description">When or what it is, e.g. <c>Served 5:30-10pm</c>.</param>
/// <param name="Items">Items in display order, without IDs (each gets a new one).</param>
public sealed record CreateMenuRequest(
    Guid? RestaurantId = null,
    string? Name = null,
    string? Description = null,
    ValueList<MenuItemRequest>? Items = null);
