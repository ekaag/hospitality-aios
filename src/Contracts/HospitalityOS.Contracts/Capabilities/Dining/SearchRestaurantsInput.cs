using System.ComponentModel;
using HospitalityOS.SharedKernel.Collections;
using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Contracts.Capabilities.Dining;

/// <summary>
/// Input of <c>Dining.SearchRestaurants</c>.
/// </summary>
/// <param name="PropertyId">The property.</param>
/// <param name="Query">Text found in the name, cuisine, or description.</param>
/// <param name="Dietary">Only restaurants serving at least one available item with all of these.</param>
public sealed record SearchRestaurantsInput(
    [property: Description("The property.")] PropertyId PropertyId,
    [property: Description("Text found in the name, cuisine, or description.")] string? Query = null,
    [property: Description("Only restaurants serving at least one available item with all of these.")] ValueList<DietaryTag>? Dietary = null);
