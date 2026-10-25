using HospitalityOS.Domain.Dining.Menus;
using HospitalityOS.Domain.Dining.Menus.Events;
using HospitalityOS.Domain.Dining.Restaurants;
using HospitalityOS.SharedKernel.Ids;
using HospitalityOS.SharedKernel.Pricing;
using HospitalityOS.SharedKernel.Results;
using HospitalityOS.SharedKernel.Tenancy;
using Xunit;

namespace HospitalityOS.UnitTests.Domain.Dining.Menus;

public sealed class MenuTests
{
    private static readonly MenuId id = new(Guid.Parse("01890a5d-ac96-774b-bcce-b30209910001"));
    private static readonly TenantId tenantId = new(Guid.Parse("01890a5d-ac96-774b-bcce-b30209910002"));
    private static readonly PropertyId propertyId = new(Guid.Parse("01890a5d-ac96-774b-bcce-b30209910003"));
    private static readonly DateTimeOffset now = new(2026, 10, 24, 12, 0, 0, TimeSpan.Zero);

    private static readonly MenuItem risotto = new(
        ItemId(1), "Wild mushroom risotto", "Arborio rice, porcini, parmesan.", new Money(24m, "USD"), "Mains", [DietaryTag.Vegetarian, DietaryTag.GlutenFree]);

    private static readonly MenuItem buddhaBowl = new(
        ItemId(2), "Buddha bowl", null, new Money(19.5m, "USD"), "Mains", [DietaryTag.Vegetarian, DietaryTag.Vegan]);

    private static readonly MenuItem salmon = new(
        ItemId(3), "Cedar-plank salmon", null, new Money(32m, "USD"), "Mains", [DietaryTag.GlutenFree]);

    private static readonly MenuItem marketFish = new(ItemId(4), "Catch of the day", "Ask your server.", Price: null, "Mains", []);

    private static readonly MenuDetails dinner = new("Dinner", [risotto, buddhaBowl, salmon, marketFish], "Served 5:30-10pm.");

    [Fact]
    public void Create_ForRestaurant_TakesTenantAndPropertyAndRaisesCreated()
    {
        Restaurant restaurant = HarbourGrill();

        Menu menu = Menu.Create(id, restaurant, dinner, now).Value;

        Assert.Equal(restaurant.Id, menu.RestaurantId);
        Assert.Equal(propertyId, menu.PropertyId);
        Assert.Equal(tenantId, menu.TenantId);
        Assert.Equal([new MenuCreated(id, propertyId, restaurant.Id, dinner, now)], menu.DomainEvents);
    }

    [Fact]
    public void Create_NormalizesItems_VeganImpliesVegetarian_CurrencyUpperCased()
    {
        MenuItem messy = new(ItemId(9), " Falafel wrap ", " ", new Money(12m, "usd"), " Lunch ", [DietaryTag.Spicy, DietaryTag.Vegan, DietaryTag.Spicy]);

        Menu menu = Menu.Create(id, HarbourGrill(), new MenuDetails("Lunch", [messy]), now).Value;

        Assert.Equal(
            new MenuItem(ItemId(9), "Falafel wrap", null, new Money(12m, "USD"), "Lunch", [DietaryTag.Vegetarian, DietaryTag.Vegan, DietaryTag.Spicy]),
            Assert.Single(menu.Details.Items));
    }

    [Fact]
    public void FindItems_Vegetarian_ReturnsVegetarianAndVeganItemsInMenuOrder()
    {
        Menu menu = Menu.Create(id, HarbourGrill(), dinner, now).Value;

        IReadOnlyList<MenuItem> vegetarian = menu.FindItems([DietaryTag.Vegetarian]);

        Assert.Equal([risotto, buddhaBowl], vegetarian);
    }

    [Fact]
    public void FindItems_WithSeveralAttributes_RequiresAll()
    {
        Menu menu = Menu.Create(id, HarbourGrill(), dinner, now).Value;

        Assert.Equal([risotto], menu.FindItems([DietaryTag.Vegetarian, DietaryTag.GlutenFree]));
    }

    [Fact]
    public void SetItemAvailability_SoldOut_HidesItemFromSearchAndRaisesUpdated()
    {
        Menu menu = CreateDinner();

        Result result = menu.SetItemAvailability(risotto.Id, false, now.AddHours(1));

        Assert.True(result.IsSuccess);
        Assert.Equal([buddhaBowl], menu.FindItems([DietaryTag.Vegetarian]));
        Assert.Equal([risotto with { IsAvailable = false }, buddhaBowl], menu.FindItems([DietaryTag.Vegetarian], availableOnly: false));
        MenuUpdated updated = Assert.IsType<MenuUpdated>(Assert.Single(menu.DomainEvents));
        Assert.Equal(menu.Details, updated.Details);
    }

    [Fact]
    public void SetItemAvailability_ToCurrentValue_IsNoOp()
    {
        Menu menu = CreateDinner();

        Result result = menu.SetItemAvailability(risotto.Id, true, now.AddHours(1));

        Assert.True(result.IsSuccess);
        Assert.Empty(menu.DomainEvents);
    }

    [Fact]
    public void SetItemAvailability_ForUnknownItem_ReturnsNotFound()
    {
        Menu menu = CreateDinner();

        Result result = menu.SetItemAvailability(ItemId(99), false, now.AddHours(1));

        Assert.Equal(ErrorKind.NotFound, result.Error.Kind);
        Assert.Empty(menu.DomainEvents);
    }

    [Fact]
    public void Create_WithInvalidItems_ReportsEveryProblem()
    {
        MenuDetails invalid = new(
            "",
            [
                new MenuItem(default, "No id"),
                new MenuItem(ItemId(1), "", Price: new Money(-1m, "USD")),
                new MenuItem(ItemId(1), "Duplicate id", Price: new Money(9.999m, "EUR")),
                new MenuItem(ItemId(2), "Trail mix", Dietary: [DietaryTag.NutFree, DietaryTag.ContainsNuts]),
            ]);

        Result<Menu> result = Menu.Create(id, HarbourGrill(), invalid, now);

        ValidationError error = Assert.IsType<ValidationError>(result.Error);
        Assert.Equal(
            [
                ("name", "menu.name_required"),
                ("items[0].id", "menu.item_id_required"),
                ("items[1].price.amount", "menu.item_price_amount_invalid"),
                ("items[1].name", "menu.item_name_required"),
                ("items[2].id", "menu.item_id_duplicate"),
                ("items[2].price.amount", "menu.item_price_amount_invalid"),
                ("items[3].dietary", "menu.item_dietary_contradictory"),
                ("items", "menu.currency_mixed"),
            ],
            error.Fields.Select(field => (field.Field, field.Code)));
    }

    [Fact]
    public void Update_ReplacingItems_RaisesSingleUpdated_AndSameMenuIsNoOp()
    {
        Menu menu = CreateDinner();
        MenuDetails trimmed = dinner with { Items = [risotto, salmon] };

        menu.Update(trimmed, now.AddHours(1));
        menu.Update(trimmed with { Name = " Dinner " }, now.AddHours(2));

        Assert.Equal([new MenuUpdated(id, trimmed, now.AddHours(1))], menu.DomainEvents);
        Assert.Equal(2, menu.Version);
    }

    private static Menu CreateDinner()
    {
        Menu menu = Menu.Create(id, HarbourGrill(), dinner, now).Value;
        menu.DequeueDomainEvents();

        return menu;
    }

    private static Restaurant HarbourGrill() =>
        Restaurant.Create(new RestaurantId(Guid.Parse("01890a5d-ac96-774b-bcce-b30209910009")), tenantId, propertyId, new RestaurantDetails("Harbour Grill"), now).Value;

    private static MenuItemId ItemId(int suffix) => new(Guid.Parse($"01890a5d-ac96-774b-bcce-b302099{suffix:D5}"));
}
