using HospitalityOS.Capabilities.Definitions;
using HospitalityOS.Capabilities.Reading;
using HospitalityOS.Contracts;
using HospitalityOS.Contracts.Capabilities.Dining;
using HospitalityOS.Domain.Dining.Menus;
using HospitalityOS.Domain.Dining.Restaurants;
using HospitalityOS.Domain.Properties.Hours;
using HospitalityOS.Domain.Properties.Properties;
using HospitalityOS.Domain.Properties.Shared;
using HospitalityOS.SharedKernel.Collections;
using HospitalityOS.SharedKernel.Results;
using DomainDietaryTag = HospitalityOS.Domain.Dining.Menus.DietaryTag;
using DomainSubjectKind = HospitalityOS.Domain.Properties.Shared.SubjectKind;

namespace HospitalityOS.Capabilities.Dining;

/// <summary>
/// <c>Dining.SearchRestaurants</c>: the property's restaurants and bars, by text and by dietary need, each with today's
/// hours (Journey 7, Demo 3).
/// </summary>
internal sealed class SearchRestaurantsCapability(
    IPropertyRepository properties,
    IRestaurantRepository restaurants,
    IMenuRepository menus,
    IOperatingHoursRepository operatingHours,
    TimeProvider timeProvider) : ICapability<SearchRestaurantsInput, SearchRestaurantsOutput>
{
    public static CapabilityDescriptor Descriptor { get; } = ReadCapability.Describe(
        "Dining.SearchRestaurants",
        "Finds the property's restaurants and bars, optionally by text in the name, cuisine, or description, and by dietary "
            + "need (only places serving at least one available dish with all the given tags). Each comes with today's hours "
            + "and whether it is open now; use Dining.GetMenu for dishes and prices.");

    public async Task<Result<SearchRestaurantsOutput>> ExecuteAsync(SearchRestaurantsInput input, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);

        Result<PropertyClock> loaded = await PropertyClock.LoadAsync(properties, input.PropertyId, timeProvider, cancellationToken).ConfigureAwait(false);

        if (loaded.IsFailure)
        {
            return loaded.Error;
        }

        PropertyClock clock = loaded.Value;
        string? text = string.IsNullOrWhiteSpace(input.Query) ? null : input.Query.Trim();
        DomainDietaryTag[] dietary = [.. (input.Dietary ?? []).Select(tag => EnumMirror.To<DomainDietaryTag>(tag))];
        HashSet<RestaurantId>? servingDietary = dietary.Length == 0 ? null : await ServingAsync(input, dietary, cancellationToken).ConfigureAwait(false);
        IReadOnlyList<Restaurant> all = await restaurants.ListForPropertyAsync(input.PropertyId, cancellationToken).ConfigureAwait(false);
        List<RestaurantEntry> entries = [];

        foreach (Restaurant restaurant in all.OrderBy(candidate => candidate.Details.Name, StringComparer.OrdinalIgnoreCase))
        {
            if ((text is not null && !Mentions(restaurant.Details, text)) || (servingDietary is not null && !servingDietary.Contains(restaurant.Id)))
            {
                continue;
            }

            OperatingHours? hours = await operatingHours
                .FindBySubjectAsync(input.PropertyId, new SubjectReference(DomainSubjectKind.Restaurant, restaurant.Id.Value), cancellationToken)
                .ConfigureAwait(false);

            entries.Add(new RestaurantEntry(
                Sources.Of(restaurant),
                restaurant.Details.Name,
                restaurant.Details.Cuisine,
                restaurant.Details.Description,
                restaurant.Details.LocationSpaceId,
                hours is null ? null : Hours.On(hours, clock.Today, clock)));
        }

        return new SearchRestaurantsOutput(clock.Property.Details.TimeZoneId, clock.LocalNow, ValueList.From(entries));
    }

    private static bool Mentions(RestaurantDetails details, string text) =>
        details.Name.Contains(text, StringComparison.OrdinalIgnoreCase)
        || (details.Cuisine?.Contains(text, StringComparison.OrdinalIgnoreCase) ?? false)
        || (details.Description?.Contains(text, StringComparison.OrdinalIgnoreCase) ?? false);

    private async Task<HashSet<RestaurantId>> ServingAsync(SearchRestaurantsInput input, DomainDietaryTag[] dietary, CancellationToken cancellationToken)
    {
        IReadOnlyList<Menu> propertyMenus = await menus.ListForPropertyAsync(input.PropertyId, cancellationToken).ConfigureAwait(false);

        return [.. propertyMenus.Where(menu => menu.FindItems(dietary).Count > 0).Select(menu => menu.RestaurantId)];
    }
}
