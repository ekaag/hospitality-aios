using HospitalityOS.Contracts.Capabilities.Dining;
using HospitalityOS.SharedKernel.Collections;
using HospitalityOS.SharedKernel.Pricing;

namespace HospitalityOS.Contracts.Api.Menus;

/// <summary>
/// A dish or drink on a menu being written.
/// </summary>
/// <param name="Id">To keep an item already on the menu, its ID (answers cite it); omit for a new item.</param>
/// <param name="Name">Display name, e.g. <c>Wild mushroom risotto</c>; required.</param>
/// <param name="Description">What it is.</param>
/// <param name="Price">Price; omit when not published, so the concierge says it does not know rather than guessing. All
/// prices on a menu use one currency.</param>
/// <param name="Category">Section of the menu, e.g. <c>Mains</c>.</param>
/// <param name="Dietary">Dietary tags; only tags the property has confirmed (absence means "not stated", not "safe").
/// Vegan implies vegetarian.</param>
/// <param name="IsAvailable">Whether it can currently be ordered; available when omitted.</param>
public sealed record MenuItemRequest(
    Guid? Id = null,
    string? Name = null,
    string? Description = null,
    Money? Price = null,
    string? Category = null,
    ValueList<DietaryTag>? Dietary = null,
    bool? IsAvailable = null);
