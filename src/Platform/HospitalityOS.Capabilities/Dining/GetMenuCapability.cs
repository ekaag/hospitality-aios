using HospitalityOS.Capabilities.Definitions;
using HospitalityOS.Capabilities.Reading;
using HospitalityOS.Contracts.Capabilities.Dining;
using HospitalityOS.Domain.Dining.Menus;
using HospitalityOS.Domain.Dining.Restaurants;
using HospitalityOS.SharedKernel.Collections;
using HospitalityOS.SharedKernel.Results;
using ContractDietaryTag = HospitalityOS.Contracts.Capabilities.Dining.DietaryTag;
using DomainDietaryTag = HospitalityOS.Domain.Dining.Menus.DietaryTag;

namespace HospitalityOS.Capabilities.Dining;

/// <summary>
/// <c>Dining.GetMenu</c>: a restaurant's menus with prices, dietary tags, and availability, optionally only the dishes
/// meeting dietary needs (Journey 7). Prices come only from here; the agent never guesses one.
/// </summary>
internal sealed class GetMenuCapability(IRestaurantRepository restaurants, IMenuRepository menus) : ICapability<GetMenuInput, GetMenuOutput>
{
    public static CapabilityDescriptor Descriptor { get; } = ReadCapability.Describe(
        "Dining.GetMenu",
        "Returns a restaurant's menus: dishes with descriptions, prices, dietary tags, and availability. With dietary tags, "
            + "returns only available dishes having all of them. Quote prices only as returned; an item without a price has none listed.");

    public async Task<Result<GetMenuOutput>> ExecuteAsync(GetMenuInput input, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);

        if (input.RestaurantId == Guid.Empty)
        {
            return ReadErrors.RestaurantNotFound;
        }

        Restaurant? restaurant = await restaurants.GetByIdAsync(new RestaurantId(input.RestaurantId), cancellationToken).ConfigureAwait(false);

        if (restaurant is null)
        {
            return ReadErrors.RestaurantNotFound;
        }

        DomainDietaryTag[] dietary = [.. (input.Dietary ?? []).Select(tag => ContractEnum.To<DomainDietaryTag>(tag))];
        IReadOnlyList<Menu> restaurantMenus = await menus.ListForRestaurantAsync(restaurant.Id, cancellationToken).ConfigureAwait(false);
        List<MenuEntry> entries = [];

        foreach (Menu menu in restaurantMenus.OrderBy(candidate => candidate.Details.Name, StringComparer.OrdinalIgnoreCase))
        {
            IReadOnlyList<MenuItem> items = dietary.Length == 0 ? menu.Details.Items : menu.FindItems(dietary);

            if (dietary.Length > 0 && items.Count == 0)
            {
                continue;
            }

            entries.Add(new MenuEntry(Sources.Of(menu), menu.Details.Name, menu.Details.Description, ValueList.From(items.Select(Entry))));
        }

        return new GetMenuOutput(Sources.Of(restaurant), restaurant.Details.Name, ValueList.From(entries));
    }

    private static MenuItemEntry Entry(MenuItem item) =>
        new(
            item.Id.Value,
            item.Name,
            item.Description,
            item.Price,
            item.Category,
            ValueList.From((item.Dietary ?? []).Select(tag => ContractEnum.To<ContractDietaryTag>(tag))),
            item.IsAvailable);
}
