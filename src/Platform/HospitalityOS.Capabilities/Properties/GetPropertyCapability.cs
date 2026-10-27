using HospitalityOS.Capabilities.Definitions;
using HospitalityOS.Capabilities.Reading;
using HospitalityOS.Contracts.Capabilities.Properties;
using HospitalityOS.Domain.Dining.Restaurants;
using HospitalityOS.Domain.Properties.Amenities;
using HospitalityOS.Domain.Properties.Properties;
using HospitalityOS.SharedKernel.Collections;
using HospitalityOS.SharedKernel.Results;
using ContractAmenityCategory = HospitalityOS.Contracts.Capabilities.Properties.AmenityCategory;

namespace HospitalityOS.Capabilities.Properties;

/// <summary>
/// <c>Property.GetProperty</c>: the property's details and what it offers, so an agent can find the amenity and
/// restaurant IDs other capabilities take.
/// </summary>
internal sealed class GetPropertyCapability(
    IPropertyRepository properties,
    IAmenityRepository amenities,
    IRestaurantRepository restaurants,
    TimeProvider timeProvider) : ICapability<GetPropertyInput, GetPropertyOutput>
{
    public static CapabilityDescriptor Descriptor { get; } = ReadCapability.Describe(
        "Property.GetProperty",
        "Returns the property's name, address, contact details, time zone, and current local time, with its amenities and "
            + "restaurants (and their IDs, for the hours, policy, and menu capabilities). Use it first when you need to know what the property offers.");

    public async Task<Result<GetPropertyOutput>> ExecuteAsync(GetPropertyInput input, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);

        Result<PropertyClock> loaded = await PropertyClock.LoadAsync(properties, input.PropertyId, timeProvider, cancellationToken).ConfigureAwait(false);

        if (loaded.IsFailure)
        {
            return loaded.Error;
        }

        PropertyClock clock = loaded.Value;
        PropertyDetails details = clock.Property.Details;
        IReadOnlyList<Amenity> propertyAmenities = await amenities.ListForPropertyAsync(input.PropertyId, cancellationToken).ConfigureAwait(false);
        IReadOnlyList<Restaurant> propertyRestaurants = await restaurants.ListForPropertyAsync(input.PropertyId, cancellationToken).ConfigureAwait(false);

        return new GetPropertyOutput(
            Sources.Of(clock.Property),
            details.Name,
            details.TimeZoneId,
            clock.LocalNow,
            new Address(details.Address.Line1, details.Address.Line2, details.Address.City, details.Address.Region, details.Address.PostalCode, details.Address.CountryCode),
            details.ContactPhone,
            details.ContactEmail,
            ValueList.From(propertyAmenities
                .OrderBy(amenity => amenity.Details.Name, StringComparer.OrdinalIgnoreCase)
                .Select(amenity => new AmenityEntry(
                    Sources.Of(amenity),
                    amenity.Details.Name,
                    ContractEnum.To<ContractAmenityCategory>(amenity.Details.Category),
                    amenity.Details.Description,
                    amenity.LocationSpaceId))),
            ValueList.From(propertyRestaurants
                .OrderBy(restaurant => restaurant.Details.Name, StringComparer.OrdinalIgnoreCase)
                .Select(restaurant => new RestaurantSummary(Sources.Of(restaurant), restaurant.Details.Name, restaurant.Details.Cuisine))));
    }
}
