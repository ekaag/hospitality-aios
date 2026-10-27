using System.Text.Json;
using HospitalityOS.Capabilities.Execution;
using HospitalityOS.Contracts.Capabilities;
using HospitalityOS.Contracts.Capabilities.Activities;
using HospitalityOS.Contracts.Capabilities.Dining;
using HospitalityOS.Contracts.Capabilities.Properties;
using HospitalityOS.Contracts.Serialization;
using HospitalityOS.Ports.Execution;
using HospitalityOS.Ports.Tenancy;
using HospitalityOS.SharedKernel.Execution;
using HospitalityOS.SharedKernel.Pricing;
using HospitalityOS.SharedKernel.Results;
using HospitalityOS.SharedKernel.Tenancy;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace HospitalityOS.IntegrationTests.Capabilities.Reading;

/// <summary>
/// The spec §11 read capabilities through the executor in the real host, against the Aurora Grand at a fixed time:
/// authoritative answers with their sources (P4), and nothing past, cancelled, unpublished, inactive, or out of effect.
/// </summary>
public sealed class ReadCapabilitiesTests : IAsyncLifetime
{
    private static readonly Actor guest = new(ActorType.Guest, "guest-1");
    private static readonly string[] vegetarianTags = ["Vegetarian"];
    private static readonly string[] veganTags = ["Vegan"];

    private readonly AuroraGrand aurora = new(new TenantId(Guid.CreateVersion7()));
    private readonly WebApplicationFactory<Program> factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
    {
        builder.UseSetting("Eventing:Dispatcher:Enabled", "false");
        builder.ConfigureTestServices(services => services.AddSingleton<TimeProvider>(new FakeTimeProvider(AuroraGrand.Now)));
    });

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() => await aurora.SaveAsync(factory.Services, Cancellation);

    public async ValueTask DisposeAsync() => await factory.DisposeAsync();

    [Fact]
    public async Task GetProperty_ReturnsDetailsAmenitiesAndRestaurantsWithSources()
    {
        GetPropertyOutput output = await ReadAsync<GetPropertyOutput>("Property.GetProperty", new { propertyId = aurora.PropertyId });

        Assert.Equal(
            (new SourceReference("Property", aurora.PropertyId.Value, 1), "Aurora Grand", "Europe/London", AuroraGrand.LocalNow, "London", "+44 20 7946 0000"),
            (output.Source, output.Name, output.TimeZone, output.LocalNow, output.Address.City, output.ContactPhone));
        Assert.Equal(
            [("Gym", aurora.Gym.Id.Value, AmenityCategory.Fitness), ("Pool", aurora.Pool.Id.Value, AmenityCategory.Pool)],
            output.Amenities.Select(amenity => (amenity.Name, amenity.Source.Id, amenity.Category)));
        Assert.Equal(["Harbour Grill", "Saffron"], output.Restaurants.Select(restaurant => restaurant.Name));
    }

    [Fact]
    public async Task GetProperty_OfAnotherTenantOrUnknown_IsNotFound()
    {
        AuroraGrand other = new(new TenantId(Guid.CreateVersion7()));
        await other.SaveAsync(factory.Services, Cancellation);

        Error error = await FailAsync("Property.GetProperty", new { propertyId = other.PropertyId });

        Assert.Equal(("property.not_found", ErrorKind.NotFound), (error.Code, error.Kind));
    }

    [Fact]
    public async Task GetOperatingHours_Today_ReturnsWeeklyHoursAndOpenNow()
    {
        GetOperatingHoursOutput output = await ReadAsync<GetOperatingHoursOutput>(
            "Property.GetOperatingHours", new { propertyId = aurora.PropertyId, subject = "Amenity", subjectId = aurora.Pool.Id.Value });

        Assert.Equal(new Subject(SubjectKind.Amenity, aurora.Pool.Id.Value), output.Subject);
        Assert.Equal(
            (new SourceReference("OperatingHours", aurora.PoolHours.Id.Value, 1), new DateOnly(2026, 10, 26), HoursBasis.Weekly, true),
            (output.Hours.Source, output.Hours.Date, output.Hours.Basis, output.Hours.IsOpenNow));
        Assert.Equal([new OpeningPeriod(AuroraGrand.LocalNow.Date.AddHours(7), AuroraGrand.LocalNow.Date.AddHours(22))], output.Hours.Periods);
    }

    [Fact]
    public async Task GetOperatingHours_OnAHolidayOrClosure_ReturnsTheExceptionWithItsNote()
    {
        GetOperatingHoursOutput christmas = await ReadAsync<GetOperatingHoursOutput>(
            "Property.GetOperatingHours", new { propertyId = aurora.PropertyId, subject = "Amenity", subjectId = aurora.Pool.Id.Value, date = AuroraGrand.Christmas });
        GetOperatingHoursOutput maintenance = await ReadAsync<GetOperatingHoursOutput>(
            "Property.GetOperatingHours", new { propertyId = aurora.PropertyId, subject = "Amenity", subjectId = aurora.Pool.Id.Value, date = AuroraGrand.MaintenanceDay });

        Assert.Equal((HoursBasis.Override, "Christmas hours", 1), (christmas.Hours.Basis, christmas.Hours.Note, christmas.Hours.Periods.Count));
        Assert.Equal((HoursBasis.Closure, "Maintenance", 0), (maintenance.Hours.Basis, maintenance.Hours.Note, maintenance.Hours.Periods.Count));
    }

    [Fact]
    public async Task GetOperatingHours_WithNoneRecorded_IsNotFoundSoTheAgentSaysItDoesNotKnow()
    {
        Error gym = await FailAsync("Property.GetOperatingHours", new { propertyId = aurora.PropertyId, subject = "Amenity", subjectId = aurora.Gym.Id.Value });
        Error property = await FailAsync("Property.GetOperatingHours", new { propertyId = aurora.PropertyId });

        Assert.Equal(["hours.not_found", "hours.not_found"], [gym.Code, property.Code]);
    }

    [Fact]
    public async Task GetOperatingHours_ForAnAmenityWithoutItsId_IsInvalid()
    {
        Error error = await FailAsync("Property.GetOperatingHours", new { propertyId = aurora.PropertyId, subject = "Amenity" });

        Assert.Equal(ErrorKind.Validation, error.Kind);
    }

    [Fact]
    public async Task GetPolicy_ReturnsOnlyActivePoliciesInEffectToday()
    {
        GetPolicyOutput output = await ReadAsync<GetPolicyOutput>("Property.GetPolicy", new { propertyId = aurora.PropertyId });

        Assert.Equal(["Children in the gym", "Pets"], output.Policies.Select(policy => policy.Title));
        PolicyEntry gym = output.Policies[0];
        Assert.Equal(
            (new SourceReference("Policy", aurora.GymChildren.Id.Value, 2), PolicyType.Children, "Guests under 16 must be accompanied by an adult.", new Subject(SubjectKind.Amenity, aurora.Gym.Id.Value)),
            (gym.Source, gym.Type, gym.Content, gym.AppliesTo));
    }

    [Fact]
    public async Task GetPolicy_ByTypeAndSubject_FiltersButKeepsPropertyWidePolicies()
    {
        GetPolicyOutput children = await ReadAsync<GetPolicyOutput>("Property.GetPolicy", new { propertyId = aurora.PropertyId, type = "Children" });
        GetPolicyOutput pool = await ReadAsync<GetPolicyOutput>("Property.GetPolicy", new { propertyId = aurora.PropertyId, subjectId = aurora.Pool.Id.Value });
        GetPolicyOutput smoking = await ReadAsync<GetPolicyOutput>("Property.GetPolicy", new { propertyId = aurora.PropertyId, type = "Smoking" });

        Assert.Equal(["Children in the gym"], children.Policies.Select(policy => policy.Title));
        Assert.Equal(["Pets"], pool.Policies.Select(policy => policy.Title));
        Assert.Empty(smoking.Policies);
    }

    [Theory]
    [InlineData("wifi password", "Wi-Fi")]
    [InlineData("What is the WiFi?", "Wi-Fi")]
    [InlineData("internet", "Wi-Fi")]
    [InlineData("where do I park the car", "Parking")]
    public async Task SearchContent_FindsPublishedContentBestFirst(string query, string expectedFirst)
    {
        SearchContentOutput output = await ReadAsync<SearchContentOutput>("Property.SearchContent", new { propertyId = aurora.PropertyId, query });

        Assert.Equal(expectedFirst, output.Results[0].Title);
        Assert.DoesNotContain(output.Results, result => result.Title == "Spa opening");
    }

    [Fact]
    public async Task SearchContent_ByTypeAndLimit_NarrowsTheResults()
    {
        SearchContentOutput directions = await ReadAsync<SearchContentOutput>("Property.SearchContent", new { propertyId = aurora.PropertyId, query = "network", type = "Directions" });
        SearchContentOutput one = await ReadAsync<SearchContentOutput>("Property.SearchContent", new { propertyId = aurora.PropertyId, query = "network", limit = 1 });

        Assert.Equal(["Parking"], directions.Results.Select(result => result.Title));
        Assert.Single(one.Results);
    }

    [Theory]
    [InlineData("the", 5)]
    [InlineData("wifi", 0)]
    [InlineData("wifi", 21)]
    public async Task SearchContent_WithNoUsableWordsOrABadLimit_IsInvalid(string query, int limit)
    {
        Error error = await FailAsync("Property.SearchContent", new { propertyId = aurora.PropertyId, query, limit });

        Assert.Equal(ErrorKind.Validation, error.Kind);
    }

    [Fact]
    public async Task SearchRestaurants_ReturnsEveryRestaurantWithTodaysHoursWhenKnown()
    {
        SearchRestaurantsOutput output = await ReadAsync<SearchRestaurantsOutput>("Dining.SearchRestaurants", new { propertyId = aurora.PropertyId });

        Assert.Equal(["Harbour Grill", "Saffron"], output.Restaurants.Select(restaurant => restaurant.Name));
        Assert.Null(output.Restaurants[0].HoursToday);
        Assert.Equal((aurora.SaffronHours.Id.Value, true), (output.Restaurants[1].HoursToday!.Source.Id, output.Restaurants[1].HoursToday!.IsOpenNow));
    }

    [Theory]
    [InlineData("indian", null, "Saffron")]
    [InlineData(null, "Vegetarian", "Saffron")]
    public async Task SearchRestaurants_ByTextOrDietaryNeed_FindsOnlyMatches(string? query, string? dietary, string expected)
    {
        SearchRestaurantsOutput output = await ReadAsync<SearchRestaurantsOutput>(
            "Dining.SearchRestaurants", new { propertyId = aurora.PropertyId, query, dietary = dietary is null ? null : new[] { dietary } });

        Assert.Equal([expected], output.Restaurants.Select(restaurant => restaurant.Name));
    }

    [Fact]
    public async Task SearchRestaurants_ForADietWhereOnlyUnavailableDishesQualify_FindsNothing()
    {
        SearchRestaurantsOutput output = await ReadAsync<SearchRestaurantsOutput>("Dining.SearchRestaurants", new { propertyId = aurora.PropertyId, dietary = veganTags });

        Assert.Empty(output.Restaurants);
    }

    [Fact]
    public async Task GetMenu_ReturnsEveryItemWithPricesTagsAndAvailability()
    {
        GetMenuOutput output = await ReadAsync<GetMenuOutput>("Dining.GetMenu", new { restaurantId = aurora.Saffron.Id.Value });

        MenuEntry dinner = Assert.Single(output.Menus);
        Assert.Equal((aurora.Saffron.Id.Value, "Saffron", aurora.SaffronDinner.Id.Value), (output.Restaurant.Id, output.RestaurantName, dinner.Source.Id));
        Assert.Equal(
            [("Paneer tikka", (Money?)new Money(14.50m, "GBP"), true), ("Lamb rogan josh", new Money(22m, "GBP"), true), ("Tarka dal", null, false)],
            dinner.Items.Select(item => (item.Name, item.Price, item.IsAvailable)));
        Assert.Equal([DietaryTag.Vegetarian], dinner.Items[0].Dietary);
    }

    [Fact]
    public async Task GetMenu_WithDietaryNeeds_ReturnsOnlyAvailableMatchingDishes()
    {
        GetMenuOutput vegetarian = await ReadAsync<GetMenuOutput>("Dining.GetMenu", new { restaurantId = aurora.Saffron.Id.Value, dietary = vegetarianTags });
        GetMenuOutput vegan = await ReadAsync<GetMenuOutput>("Dining.GetMenu", new { restaurantId = aurora.Saffron.Id.Value, dietary = veganTags });

        Assert.Equal([AuroraGrand.PaneerTikka.Value], Assert.Single(vegetarian.Menus).Items.Select(item => item.ItemId));
        Assert.Empty(vegan.Menus);
    }

    [Fact]
    public async Task GetMenu_OfAnUnknownRestaurant_IsNotFound()
    {
        Error unknown = await FailAsync("Dining.GetMenu", new { restaurantId = Guid.CreateVersion7() });
        Error empty = await FailAsync("Dining.GetMenu", new { restaurantId = Guid.Empty });

        Assert.Equal(["restaurant.not_found", "restaurant.not_found"], [unknown.Code, empty.Code]);
    }

    [Fact]
    public async Task SearchPropertyEvents_ReturnsOngoingAndUpcomingEarliestFirst_NeverPastOrCancelled()
    {
        SearchPropertyEventsOutput output = await ReadAsync<SearchPropertyEventsOutput>("Events.SearchPropertyEvents", new { propertyId = aurora.PropertyId });

        Assert.Equal(["Kids club", "Jazz night", "Cooking class"], output.Events.Select(propertyEvent => propertyEvent.Title));
        EventEntry cooking = output.Events[2];
        Assert.Equal(
            (new SourceReference("PropertyEvent", aurora.CookingClass.Id.Value, 1), EventAudience.AdultsOnly, (Money?)new Money(45m, "GBP"), true),
            (cooking.Source, cooking.Audience, cooking.Price, cooking.BookingRequired));
    }

    [Fact]
    public async Task SearchPropertyEvents_ByWindowCategoryAudienceAndLimit_Narrows()
    {
        SearchPropertyEventsOutput tonight = await ReadAsync<SearchPropertyEventsOutput>(
            "Events.SearchPropertyEvents", new { propertyId = aurora.PropertyId, to = AuroraGrand.LocalNow.Date.AddDays(1) });
        SearchPropertyEventsOutput food = await ReadAsync<SearchPropertyEventsOutput>("Events.SearchPropertyEvents", new { propertyId = aurora.PropertyId, category = "FoodAndDrink" });
        SearchPropertyEventsOutput children = await ReadAsync<SearchPropertyEventsOutput>("Events.SearchPropertyEvents", new { propertyId = aurora.PropertyId, audience = "Children" });
        SearchPropertyEventsOutput later = await ReadAsync<SearchPropertyEventsOutput>(
            "Events.SearchPropertyEvents", new { propertyId = aurora.PropertyId, from = AuroraGrand.LocalNow.AddHours(1), limit = 1 });

        Assert.Equal(["Kids club", "Jazz night"], tonight.Events.Select(propertyEvent => propertyEvent.Title));
        Assert.Equal(["Cooking class"], food.Events.Select(propertyEvent => propertyEvent.Title));
        Assert.Equal(["Kids club", "Jazz night"], children.Events.Select(propertyEvent => propertyEvent.Title));
        Assert.Equal(["Jazz night"], later.Events.Select(propertyEvent => propertyEvent.Title));
    }

    [Fact]
    public async Task SearchPropertyEvents_FromInThePast_StillNeverReturnsPastEvents()
    {
        SearchPropertyEventsOutput output = await ReadAsync<SearchPropertyEventsOutput>(
            "Events.SearchPropertyEvents", new { propertyId = aurora.PropertyId, from = AuroraGrand.LocalNow.Date });

        Assert.DoesNotContain(output.Events, propertyEvent => propertyEvent.Title == "Morning yoga");
    }

    private async Task<Result<CapabilityResponse>> ExecuteAsync(string capability, object input)
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().Set(aurora.TenantId);
        scope.ServiceProvider.GetRequiredService<OperationContext>().ActAs(guest);

        return await scope.ServiceProvider.GetRequiredService<ICapabilityExecutor>()
            .ExecuteAsync(new CapabilityRequest(capability, JsonSerializer.SerializeToElement(input, ContractJson.Options)), Cancellation);
    }

    private async Task<TOutput> ReadAsync<TOutput>(string capability, object input)
    {
        Result<CapabilityResponse> result = await ExecuteAsync(capability, input);
        Assert.True(result.IsSuccess, result.IsFailure ? $"{result.Error.Code}: {result.Error.Message}" : null);

        return Assert.IsType<TOutput>(result.Value.Output.Deserialize<TOutput>(ContractJson.Options));
    }

    private async Task<Error> FailAsync(string capability, object input)
    {
        Result<CapabilityResponse> result = await ExecuteAsync(capability, input);
        Assert.True(result.IsFailure, "Expected a failure.");

        return result.Error;
    }
}
