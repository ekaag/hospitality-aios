using HospitalityOS.Application.Administration;
using HospitalityOS.Application.Auditing;
using HospitalityOS.Contracts;
using HospitalityOS.Contracts.Api.Menus;
using HospitalityOS.Domain.Dining.Menus;
using HospitalityOS.Domain.Dining.Restaurants;
using HospitalityOS.Ports.Bindings;
using HospitalityOS.Ports.Ids;
using HospitalityOS.SharedKernel.Collections;
using HospitalityOS.SharedKernel.Ids;
using HospitalityOS.SharedKernel.Results;
using ContractTag = HospitalityOS.Contracts.Capabilities.Dining.DietaryTag;
using DomainTag = HospitalityOS.Domain.Dining.Menus.DietaryTag;

namespace HospitalityOS.Application.Menus;

internal sealed class MenuAdministration(
    IMenuRepository menus,
    IRestaurantRepository restaurants,
    AuditedCommit commit,
    IBindingResolver<IIdGenerator> idGenerators,
    TimeProvider timeProvider) : IMenuAdministration
{
    private static readonly Error itemNotFound = Error.NotFound("menu.item_not_found", "The item is not on this menu.");

    public async Task<Result<MenuResponse>> CreateAsync(CreateMenuRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        Result<Restaurant> restaurant = await RestaurantAsync(request.RestaurantId, cancellationToken).ConfigureAwait(false);

        if (restaurant.IsFailure)
        {
            return await commit.RejectAsync(AdministrationActions.MenuCreate, restaurant.Error, null, cancellationToken).ConfigureAwait(false);
        }

        RequestFields fields = new();
        MenuDetails details = Details(request.Name, request.Description, request.Items, [], fields);
        MenuId id = new(idGenerators.Resolve().NewId());
        Result<Menu> created = fields.Merge(Menu.Create(id, restaurant.Value, details, timeProvider.GetUtcNow()));

        return await commit.CreateAsync(
            AdministrationActions.MenuCreate,
            created,
            menu => menus.AddAsync(menu, cancellationToken),
            menu => Target(menu.Id.Value, menu.PropertyId),
            Response,
            cancellationToken).ConfigureAwait(false);
    }

    public Task<Result<MenuResponse>> UpdateAsync(Guid id, UpdateMenuRequest request, long? expectedVersion, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return ChangeAsync(
            AdministrationActions.MenuUpdate,
            id,
            expectedVersion,
            menu =>
            {
                RequestFields fields = new();
                MenuDetails details = Details(request.Name, request.Description, request.Items, [.. menu.Details.Items.Select(item => item.Id)], fields);

                return fields.Apply(() => menu.Update(details, timeProvider.GetUtcNow()));
            },
            cancellationToken);
    }

    public Task<Result<MenuResponse>> SetItemAvailabilityAsync(Guid id, Guid itemId, SetItemAvailabilityRequest request, long? expectedVersion, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return ChangeAsync(
            AdministrationActions.MenuSetItemAvailability,
            id,
            expectedVersion,
            menu =>
            {
                if (itemId == Guid.Empty)
                {
                    return itemNotFound;
                }

                if (request.IsAvailable is not { } isAvailable)
                {
                    return Error.Validation("isAvailable", "menu.item_available_required", "isAvailable is required.");
                }

                return menu.SetItemAvailability(new MenuItemId(itemId), isAvailable, timeProvider.GetUtcNow());
            },
            cancellationToken);
    }

    public async Task<Result<MenuResponse>> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        Menu? menu = await FindAsync(id, cancellationToken).ConfigureAwait(false);

        return menu is null ? AdministrationErrors.MenuNotFound : Response(menu);
    }

    public async Task<IReadOnlyList<MenuResponse>> ListAsync(Guid restaurantId, CancellationToken cancellationToken)
    {
        if (restaurantId == Guid.Empty)
        {
            return [];
        }

        IReadOnlyList<Menu> found = await menus.ListForRestaurantAsync(new RestaurantId(restaurantId), cancellationToken).ConfigureAwait(false);

        return [.. found.OrderBy(menu => menu.Details.Name, StringComparer.OrdinalIgnoreCase).Select(Response)];
    }

    private async Task<Result<MenuResponse>> ChangeAsync(string action, Guid id, long? expectedVersion, Func<Menu, Result> change, CancellationToken cancellationToken)
    {
        Menu? menu = await FindAsync(id, cancellationToken).ConfigureAwait(false);

        return await commit.ChangeAsync(
            action,
            Target(id, menu?.PropertyId),
            menu,
            AdministrationErrors.MenuNotFound,
            expectedVersion,
            found => Task.FromResult(change(found)),
            Response,
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// The restaurant a new menu is for, which must exist in the caller's tenant; the menu takes its property.
    /// </summary>
    private async Task<Result<Restaurant>> RestaurantAsync(Guid? id, CancellationToken cancellationToken)
    {
        if (id is not { } restaurantId)
        {
            return Error.Validation("restaurantId", "menu.restaurant_required", "restaurantId is required.");
        }

        Restaurant? restaurant = restaurantId == Guid.Empty ? null : await restaurants.GetByIdAsync(new RestaurantId(restaurantId), cancellationToken).ConfigureAwait(false);

        return restaurant is null ? Error.Validation("restaurantId", "menu.restaurant_not_found", "There is no such restaurant.") : restaurant;
    }

    // An empty GUID cannot be a menu ID, so it names no menu.
    private Task<Menu?> FindAsync(Guid id, CancellationToken cancellationToken) =>
        id == Guid.Empty ? Task.FromResult<Menu?>(null) : menus.GetByIdAsync(new MenuId(id), cancellationToken);

    private static AuditTarget Target(Guid id, PropertyId? propertyId) => new(nameof(Menu), id.ToString(), propertyId);

    // The domain validates every field, including missing text, so absent values pass through as null; a missing item
    // becomes a null entry, which the domain reports by position.
    private MenuDetails Details(string? name, string? description, ValueList<MenuItemRequest>? items, HashSet<MenuItemId> onMenu, RequestFields fields) =>
        new(name!, ValueList.From((items ?? []).Select((item, index) => item is null ? null! : Item(item, $"items[{index}]", onMenu, fields))), description);

    // Item IDs are platform-owned: a new item gets one, and an item sent with an ID must already be on the menu.
    private MenuItem Item(MenuItemRequest item, string path, HashSet<MenuItemId> onMenu, RequestFields fields)
    {
        MenuItemId id;

        if (item.Id is { } given && given != Guid.Empty && onMenu.Contains(new MenuItemId(given)))
        {
            id = new MenuItemId(given);
        }
        else
        {
            if (item.Id is not null)
            {
                fields.Add(path + ".id", "menu.item_not_found", "The item is not on this menu; omit the ID for a new item.");
            }

            id = new MenuItemId(idGenerators.Resolve().NewId());
        }

        return new MenuItem(
            id,
            item.Name!,
            item.Description,
            item.Price,
            item.Category,
            ValueList.From((item.Dietary ?? []).Select(tag => EnumMirror.To<DomainTag>(tag))),
            item.IsAvailable ?? true);
    }

    private static MenuResponse Response(Menu menu)
    {
        MenuDetails details = menu.Details;

        return new MenuResponse(
            menu.Id.Value,
            menu.PropertyId,
            menu.RestaurantId.Value,
            details.Name,
            details.Description,
            ValueList.From(details.Items.Select(item => new MenuItemResponse(
                item.Id.Value,
                item.Name,
                item.Description,
                item.Price,
                item.Category,
                ValueList.From((item.Dietary ?? []).Select(tag => EnumMirror.To<ContractTag>(tag))),
                item.IsAvailable))),
            menu.Version);
    }
}
