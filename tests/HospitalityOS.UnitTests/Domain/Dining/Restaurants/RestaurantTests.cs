using HospitalityOS.Domain.Dining.Restaurants;
using HospitalityOS.Domain.Dining.Restaurants.Events;
using HospitalityOS.SharedKernel.Ids;
using HospitalityOS.SharedKernel.Results;
using HospitalityOS.SharedKernel.Tenancy;
using Xunit;

namespace HospitalityOS.UnitTests.Domain.Dining.Restaurants;

public sealed class RestaurantTests
{
    private static readonly RestaurantId id = new(Guid.Parse("01890a5d-ac96-774b-bcce-b30209909001"));
    private static readonly TenantId tenantId = new(Guid.Parse("01890a5d-ac96-774b-bcce-b30209909002"));
    private static readonly PropertyId propertyId = new(Guid.Parse("01890a5d-ac96-774b-bcce-b30209909003"));
    private static readonly SpaceId lobbyLevel = new(Guid.Parse("01890a5d-ac96-774b-bcce-b30209909004"));
    private static readonly DateTimeOffset now = new(2026, 10, 24, 12, 0, 0, TimeSpan.Zero);

    private static readonly RestaurantDetails harbourGrill = new("Harbour Grill", "Seafood", "Waterfront dining.", lobbyLevel);

    [Fact]
    public void Create_WithValidDetails_CreatesRestaurantAndRaisesCreated()
    {
        Restaurant restaurant = Restaurant.Create(id, tenantId, propertyId, harbourGrill, now).Value;

        Assert.Equal(harbourGrill, restaurant.Details);
        Assert.Equal([new RestaurantCreated(id, propertyId, harbourGrill, now)], restaurant.DomainEvents);
    }

    [Fact]
    public void Create_WithInvalidFields_ReportsAll()
    {
        Result<Restaurant> result = Restaurant.Create(id, tenantId, propertyId, new RestaurantDetails("", new string('x', 201)), now);

        ValidationError error = Assert.IsType<ValidationError>(result.Error);
        Assert.Equal(
            [("name", "restaurant.name_required"), ("cuisine", "restaurant.cuisine_too_long")],
            error.Fields.Select(field => (field.Field, field.Code)));
    }

    [Fact]
    public void Update_WithChangedDetails_RaisesUpdated_AndSameDetailsIsNoOp()
    {
        Restaurant restaurant = Restaurant.Create(id, tenantId, propertyId, harbourGrill, now).Value;
        restaurant.DequeueDomainEvents();
        RestaurantDetails moved = harbourGrill with { LocationSpaceId = null };

        restaurant.Update(moved, now.AddHours(1));
        restaurant.Update(moved with { Name = " Harbour Grill " }, now.AddHours(2));

        Assert.Equal([new RestaurantUpdated(id, moved, now.AddHours(1))], restaurant.DomainEvents);
    }
}
