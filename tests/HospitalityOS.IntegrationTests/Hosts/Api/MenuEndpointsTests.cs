using System.Net;
using System.Net.Http.Json;
using HospitalityOS.Contracts.Api.Menus;
using HospitalityOS.Contracts.Api.Properties;
using HospitalityOS.Contracts.Api.Restaurants;
using HospitalityOS.Contracts.Capabilities.Dining;
using HospitalityOS.Contracts.Serialization;
using HospitalityOS.Ports.Auditing;
using HospitalityOS.SharedKernel.Pricing;
using HospitalityOS.SharedKernel.Tenancy;
using Xunit;

namespace HospitalityOS.IntegrationTests.Hosts.Api;

/// <summary>
/// Journeys 3 and 7 through the real host: an administrator writes a restaurant's menu, replaces it keeping item IDs,
/// and marks a dish sold out; the concierge's dietary answers follow each change.
/// </summary>
public sealed class MenuEndpointsTests : IAsyncDisposable
{
    private const string Route = "/api/v1/menus";

    private static readonly string[] vegetarian = ["Vegetarian"];
    private static readonly string[] contradictory = ["NutFree", "ContainsNuts"];

    private readonly StaffApi api = new();
    private readonly TenantId tenant = new(Guid.CreateVersion7());

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    public ValueTask DisposeAsync() => api.DisposeAsync();

    [Fact]
    public async Task Create_GivesItemsIDs_NormalizesDietaryTags_AndTheConciergeFindsVegetarianDishes()
    {
        (PropertyResponse property, RestaurantResponse grill) = await CreateRestaurantAsync();
        using HttpClient admin = api.Staff(tenant);

        using HttpResponseMessage response = await admin.PostAsJsonAsync(Route, DinnerMenu(grill), Cancellation);

        MenuResponse dinner = await StaffApi.CreatedAsync<MenuResponse>(response, Cancellation);
        Assert.Equal((property.Id, grill.Id, "Dinner", 1L), (dinner.PropertyId, dinner.RestaurantId, dinner.Name, dinner.Version));
        Assert.All(dinner.Items, item => Assert.NotEqual(Guid.Empty, item.Id));
        MenuItemResponse risotto = dinner.Items[0];
        Assert.Equal([DietaryTag.Vegetarian, DietaryTag.Vegan], risotto.Dietary);
        Assert.Equal(new Money(18.50m, "GBP"), risotto.Price);
        Assert.Single(await api.EventsAsync(tenant, Cancellation), message => message.EventType == "MenuCreated" && message.AggregateId == dinner.Id);
        Assert.Contains(await api.AuditAsync(tenant, Cancellation), audit => (audit.Action, audit.TargetId, audit.PropertyId) == ("Menu.Create", dinner.Id.ToString(), property.Id));

        GetMenuOutput vegetarian = await AskForVegetarianAsync(grill);
        MenuEntry menu = Assert.Single(vegetarian.Menus);
        Assert.Equal((dinner.Id, 1L), (menu.Source.Id, menu.Source.Version));
        Assert.Equal([risotto.Id], menu.Items.Select(item => item.ItemId));
    }

    [Fact]
    public async Task Update_KeepsTheIDsSentBack_GivesNewItemsNewOnes_AndRemovesItemsLeftOut()
    {
        (_, RestaurantResponse grill) = await CreateRestaurantAsync();
        using HttpClient admin = api.Staff(tenant);
        MenuResponse dinner = await CreateMenuAsync(admin, grill);
        MenuItemResponse risotto = dinner.Items[0];

        using HttpResponseMessage response = await StaffApi.SendAsync(
            admin,
            HttpMethod.Put,
            $"{Route}/{dinner.Id}",
            new { name = "Dinner", items = new object[] { new { id = risotto.Id, name = "Truffle risotto", dietary = vegetarian }, new { name = "Sticky toffee pudding" } } },
            "\"1\"",
            Cancellation);

        MenuResponse updated = await StaffApi.OkAsync<MenuResponse>(response, Cancellation);
        Assert.Equal(2L, updated.Version);
        Assert.Equal((risotto.Id, "Truffle risotto"), (updated.Items[0].Id, updated.Items[0].Name));
        Assert.DoesNotContain(updated.Items[1].Id, dinner.Items.Select(item => item.Id));
        Assert.Equal(["Truffle risotto", "Sticky toffee pudding"], updated.Items.Select(item => item.Name));
        Assert.Single(await api.EventsAsync(tenant, Cancellation), message => message.EventType == "MenuUpdated");
    }

    [Fact]
    public async Task Update_WithAnItemIDNotOnTheMenu_IsRejectedByPosition_AndChangesNothing()
    {
        (_, RestaurantResponse grill) = await CreateRestaurantAsync();
        using HttpClient admin = api.Staff(tenant);
        MenuResponse dinner = await CreateMenuAsync(admin, grill);
        MenuResponse lunch = await CreateMenuAsync(admin, grill, "Lunch");

        using HttpResponseMessage response = await admin.PutAsJsonAsync(
            $"{Route}/{dinner.Id}", new { name = "Dinner", items = new object[] { new { name = "Soup" }, new { id = lunch.Items[0].Id, name = "Stolen risotto" } } }, Cancellation);

        Assert.Equal(["menu.item_not_found"], await StaffApi.FieldCodesAsync(response, Cancellation));
        MenuResponse? unchanged = await admin.GetFromJsonAsync<MenuResponse>($"{Route}/{dinner.Id}", ContractJson.Options, Cancellation);
        Assert.Equal(dinner.Items, unchanged!.Items);
        Assert.Equal(1L, unchanged.Version);
        Assert.Contains(await api.AuditAsync(tenant, Cancellation), audit => (audit.Action, audit.Outcome, audit.Detail, audit.PropertyId) == ("Menu.Update", AuditOutcome.Failed, "validation.failed", dinner.PropertyId));
    }

    [Fact]
    public async Task SetItemAvailability_SoldOut_TakesTheDishOutOfDietaryAnswers()
    {
        (PropertyResponse property, RestaurantResponse grill) = await CreateRestaurantAsync();
        using HttpClient admin = api.Staff(tenant);
        MenuResponse dinner = await CreateMenuAsync(admin, grill);
        MenuItemResponse risotto = dinner.Items[0];

        using HttpResponseMessage response = await StaffApi.SendAsync(
            admin, HttpMethod.Put, $"{Route}/{dinner.Id}/items/{risotto.Id}/availability", new { isAvailable = false }, "\"1\"", Cancellation);

        MenuResponse updated = await StaffApi.OkAsync<MenuResponse>(response, Cancellation);
        Assert.Equal((false, 2L), (updated.Items[0].IsAvailable, updated.Version));
        Assert.Empty((await AskForVegetarianAsync(grill)).Menus.SelectMany(menu => menu.Items));
        SearchRestaurantsOutput restaurants = await api.AskAsGuestAsync<SearchRestaurantsOutput>(
            tenant, "Dining.SearchRestaurants", new { propertyId = property.Id.Value, dietary = vegetarian }, Cancellation);
        Assert.Empty(restaurants.Restaurants);
        Assert.Contains(await api.AuditAsync(tenant, Cancellation), audit => (audit.Action, audit.Outcome) == ("Menu.SetItemAvailability", AuditOutcome.Succeeded));
    }

    [Fact]
    public async Task SetItemAvailability_ForAnUnknownItem_Is404_AndWithoutAValue_Is400()
    {
        (_, RestaurantResponse grill) = await CreateRestaurantAsync();
        using HttpClient admin = api.Staff(tenant);
        MenuResponse dinner = await CreateMenuAsync(admin, grill);

        using HttpResponseMessage unknown = await admin.PutAsJsonAsync($"{Route}/{dinner.Id}/items/{Guid.CreateVersion7()}/availability", new { isAvailable = false }, Cancellation);
        using HttpResponseMessage missing = await admin.PutAsJsonAsync($"{Route}/{dinner.Id}/items/{dinner.Items[0].Id}/availability", new { }, Cancellation);

        Assert.Equal((HttpStatusCode.NotFound, "menu.item_not_found"), await StaffApi.ProblemAsync(unknown, Cancellation));
        Assert.Equal(["menu.item_available_required"], await StaffApi.FieldCodesAsync(missing, Cancellation));
    }

    [Fact]
    public async Task Create_ForARestaurantOfAnotherTenant_IsARestaurantError_AndItemProblemsAreReportedByPosition()
    {
        (_, RestaurantResponse grill) = await CreateRestaurantAsync();
        TenantId other = new(Guid.CreateVersion7());
        using HttpClient otherAdmin = api.Staff(other);
        using HttpClient admin = api.Staff(tenant);

        using HttpResponseMessage foreign = await otherAdmin.PostAsJsonAsync(Route, DinnerMenu(grill), Cancellation);
        using HttpResponseMessage noRestaurant = await admin.PostAsJsonAsync(Route, new { name = "Dinner" }, Cancellation);
        using HttpResponseMessage badItems = await admin.PostAsJsonAsync(
            Route,
            new
            {
                restaurantId = grill.Id,
                name = "Dinner",
                items = new object?[]
                {
                    new { name = "Soup", price = new { amount = 7, currencyCode = "GBP" } },
                    new { price = new { amount = 9, currencyCode = "EUR" } },
                    new { name = "Salad", dietary = contradictory },
                },
            },
            Cancellation);

        Assert.Equal(["menu.restaurant_not_found"], await StaffApi.FieldCodesAsync(foreign, Cancellation));
        Assert.Equal(["menu.restaurant_required"], await StaffApi.FieldCodesAsync(noRestaurant, Cancellation));
        Assert.Equal(["menu.item_name_required", "menu.item_dietary_contradictory", "menu.currency_mixed"], await StaffApi.FieldCodesAsync(badItems, Cancellation));
    }

    [Fact]
    public async Task List_ReturnsTheRestaurantsMenus_ByName_AndManagersCannotChangeThem()
    {
        (PropertyResponse property, RestaurantResponse grill) = await CreateRestaurantAsync();
        RestaurantResponse bar = await CreateRestaurantAsync(property, "Sky Bar");
        using HttpClient admin = api.Staff(tenant);
        MenuResponse dinner = await CreateMenuAsync(admin, grill);
        await CreateMenuAsync(admin, grill, "brunch");
        await CreateMenuAsync(admin, bar, "Cocktails");
        using HttpClient manager = api.Staff(tenant, "Manager", "lee");

        IReadOnlyList<MenuResponse>? listed = await manager.GetFromJsonAsync<IReadOnlyList<MenuResponse>>($"{Route}?restaurantId={grill.Id}", ContractJson.Options, Cancellation);
        using HttpResponseMessage denied = await manager.PutAsJsonAsync($"{Route}/{dinner.Id}/items/{dinner.Items[0].Id}/availability", new { isAvailable = false }, Cancellation);

        Assert.Equal(["brunch", "Dinner"], listed!.Select(menu => menu.Name));
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        Assert.Contains(await api.AuditAsync(tenant, Cancellation), audit => (audit.Action, audit.Outcome, audit.Actor.Id) == ("Menu.SetItemAvailability", AuditOutcome.Denied, "lee"));
    }

    private static object DinnerMenu(RestaurantResponse restaurant, string name = "Dinner") =>
        new
        {
            restaurantId = restaurant.Id,
            name,
            items = new object[]
            {
                new { name = "Wild mushroom risotto", price = new { amount = 18.50m, currencyCode = "gbp" }, category = "Mains", dietary = new[] { "Vegan" } },
                new { name = "Grilled sea bass", price = new { amount = 26, currencyCode = "GBP" }, category = "Mains", dietary = new[] { "GlutenFree" } },
            },
        };

    private Task<GetMenuOutput> AskForVegetarianAsync(RestaurantResponse restaurant) =>
        api.AskAsGuestAsync<GetMenuOutput>(tenant, "Dining.GetMenu", new { restaurantId = restaurant.Id, dietary = vegetarian }, Cancellation);

    private async Task<(PropertyResponse Property, RestaurantResponse Restaurant)> CreateRestaurantAsync()
    {
        PropertyResponse property = await api.CreatePropertyAsync(tenant, Cancellation);

        return (property, await CreateRestaurantAsync(property, "Harbour Grill"));
    }

    private async Task<RestaurantResponse> CreateRestaurantAsync(PropertyResponse property, string name)
    {
        using HttpClient admin = api.Staff(tenant);
        using HttpResponseMessage response = await admin.PostAsJsonAsync("/api/v1/restaurants", new { propertyId = property.Id.Value, name }, Cancellation);

        return await StaffApi.CreatedAsync<RestaurantResponse>(response, Cancellation);
    }

    private static async Task<MenuResponse> CreateMenuAsync(HttpClient admin, RestaurantResponse restaurant, string name = "Dinner")
    {
        using HttpResponseMessage response = await admin.PostAsJsonAsync(Route, DinnerMenu(restaurant, name), Cancellation);

        return await StaffApi.CreatedAsync<MenuResponse>(response, Cancellation);
    }
}
