using HospitalityOS.SharedKernel.Collections;

namespace HospitalityOS.Contracts.Capabilities.Dining;

/// <summary>
/// Output of <c>Dining.SearchRestaurants</c>.
/// </summary>
/// <param name="TimeZone">IANA time zone of the property.</param>
/// <param name="LocalNow">The current property-local time.</param>
/// <param name="Restaurants">Matching restaurants, by name.</param>
public sealed record SearchRestaurantsOutput(
    string TimeZone,
    DateTime LocalNow,
    ValueList<RestaurantEntry> Restaurants);
