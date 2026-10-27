using HospitalityOS.SharedKernel.Collections;

namespace HospitalityOS.Contracts.Capabilities.Properties;

/// <summary>
/// Output of <c>Property.GetProperty</c>: the property and what it offers.
/// </summary>
/// <param name="Source">The property record.</param>
/// <param name="Name">Name.</param>
/// <param name="TimeZone">IANA time zone; all local times are in it.</param>
/// <param name="LocalNow">The current property-local time.</param>
/// <param name="Address">Address.</param>
/// <param name="ContactPhone">Phone.</param>
/// <param name="ContactEmail">Email.</param>
/// <param name="Amenities">Amenities, by name.</param>
/// <param name="Restaurants">Restaurants and bars, by name.</param>
public sealed record GetPropertyOutput(
    SourceReference Source,
    string Name,
    string TimeZone,
    DateTime LocalNow,
    Address Address,
    string? ContactPhone,
    string? ContactEmail,
    ValueList<AmenityEntry> Amenities,
    ValueList<RestaurantSummary> Restaurants);
