using System.ComponentModel;
using HospitalityOS.SharedKernel.Collections;

namespace HospitalityOS.Contracts.Capabilities.Dining;

/// <summary>
/// Input of <c>Dining.GetMenu</c>.
/// </summary>
/// <param name="RestaurantId">The restaurant.</param>
/// <param name="Dietary">Only available items with all of these; every item when omitted.</param>
public sealed record GetMenuInput(
    [property: Description("The restaurant.")] Guid RestaurantId,
    [property: Description("Only available items with all of these; every item when omitted.")] ValueList<DietaryTag>? Dietary = null);
