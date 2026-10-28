using HospitalityOS.SharedKernel.Collections;

namespace HospitalityOS.Contracts.Api.Menus;

/// <summary>
/// <c>PUT /api/v1/menus/{id}</c>: replaces the whole menu. Items sent with their ID keep it; items sent without one are
/// new; items left out are removed.
/// Every field may be omitted: missing required fields are reported by validation, field by field.
/// </summary>
/// <param name="Name">Display name; required.</param>
/// <param name="Description">When or what it is.</param>
/// <param name="Items">Items in display order.</param>
public sealed record UpdateMenuRequest(
    string? Name = null,
    string? Description = null,
    ValueList<MenuItemRequest>? Items = null);
