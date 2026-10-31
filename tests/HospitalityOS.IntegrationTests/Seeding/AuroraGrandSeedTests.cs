using System.Net;
using System.Net.Http.Json;
using HospitalityOS.Contracts.Api.Services;
using HospitalityOS.Contracts.Capabilities;
using HospitalityOS.Contracts.Capabilities.Activities;
using HospitalityOS.Contracts.Capabilities.Dining;
using HospitalityOS.Contracts.Capabilities.Properties;
using HospitalityOS.Contracts.Serialization;
using HospitalityOS.Domain.Guests.StayContexts;
using HospitalityOS.IntegrationRuntime.References;
using HospitalityOS.IntegrationRuntime.Stays;
using HospitalityOS.IntegrationTests.Hosts.Api;
using HospitalityOS.Ports.Auditing;
using HospitalityOS.Ports.Execution;
using HospitalityOS.Ports.Tenancy;
using HospitalityOS.Seeding;
using HospitalityOS.SharedKernel.Execution;
using HospitalityOS.SharedKernel.Tenancy;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace HospitalityOS.IntegrationTests.Seeding;

/// <summary>
/// The Aurora Grand as a host seeded at startup sees it, at <see cref="StaffApi.Now"/> (Tuesday 27 October 2026, 09:30
/// in London): each Phase 1 demo (spec §43) has the canonical data it needs, reached the way the concierge or staff
/// reach it; the seed went through administration like any other change; and seeding again changes nothing.
/// </summary>
public sealed class AuroraGrandSeedTests : IAsyncDisposable
{
    private static readonly DateOnly today = new(2026, 10, 27);
    private static readonly string[] vegetarian = ["Vegetarian"];

    private readonly StaffApi api = new(seedAuroraGrand: true);

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    private static TenantId Tenant => AuroraGrand.TenantId;

    public ValueTask DisposeAsync() => api.DisposeAsync();

    [Fact]
    public async Task Demo1_ThePoolClosesAt10Tonight()
    {
        GetOperatingHoursOutput pool = await api.AskAsGuestAsync<GetOperatingHoursOutput>(
            Tenant, "Property.GetOperatingHours", new { propertyId = AuroraGrand.PropertyId.Value, subject = "Amenity", subjectId = AuroraGrand.Pool }, Cancellation);

        OpeningPeriod period = Assert.Single(pool.Hours.Periods);
        Assert.Equal((today.ToDateTime(new TimeOnly(22, 0)), true, AuroraGrand.PoolHours), (period.End, pool.Hours.IsOpenNow, pool.Hours.Source.Id));
    }

    [Fact]
    public async Task Demo2_TheGymsChildrenPolicyAnswersForA12YearOld()
    {
        GetPolicyOutput found = await api.AskAsGuestAsync<GetPolicyOutput>(
            Tenant, "Property.GetPolicy", new { propertyId = AuroraGrand.PropertyId.Value, type = "Children", subjectId = AuroraGrand.Gym }, Cancellation);

        PolicyEntry policy = Assert.Single(found.Policies);
        Assert.Equal((AuroraGrand.GymChildrenPolicy, new Subject(SubjectKind.Amenity, AuroraGrand.Gym)), (policy.Source.Id, policy.AppliesTo));
        Assert.Contains("aged 12 to 15 may use the gym when accompanied by an adult", policy.Content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Demo3_VegetarianFoodIsFoundOnBothRestaurantsMenus()
    {
        SearchRestaurantsOutput restaurants = await api.AskAsGuestAsync<SearchRestaurantsOutput>(
            Tenant, "Dining.SearchRestaurants", new { propertyId = AuroraGrand.PropertyId.Value, dietary = vegetarian }, Cancellation);
        GetMenuOutput saffron = await api.AskAsGuestAsync<GetMenuOutput>(
            Tenant, "Dining.GetMenu", new { restaurantId = AuroraGrand.Saffron, dietary = vegetarian }, Cancellation);

        Assert.Equal(new[] { AuroraGrand.Saffron, AuroraGrand.HarbourGrill }.Order(), restaurants.Restaurants.Select(restaurant => restaurant.Source.Id).Order());
        MenuEntry dinner = Assert.Single(saffron.Menus);
        Assert.Equal(AuroraGrand.SaffronDinner, dinner.Source.Id);
        Assert.Equal([AuroraGrand.PaneerTikka, AuroraGrand.VegetableBiryani, AuroraGrand.TarkaDal, AuroraGrand.MangoKulfi], dinner.Items.Select(item => item.ItemId));
    }

    [Fact]
    public async Task Demo4_TonightHasTheWineTastingThenJazzNight()
    {
        SearchPropertyEventsOutput tonight = await api.AskAsGuestAsync<SearchPropertyEventsOutput>(
            Tenant, "Events.SearchPropertyEvents", new { propertyId = AuroraGrand.PropertyId.Value, to = today.ToDateTime(new TimeOnly(23, 59)) }, Cancellation);

        Assert.Equal([AuroraGrand.WineTasting, AuroraGrand.JazzNight], tonight.Events.Select(entry => entry.Source.Id));
        Assert.Equal((today.ToDateTime(new TimeOnly(20, 0)), AuroraGrand.LobbyBar), (tonight.Events[1].LocalStart, tonight.Events[1].LocationSpaceId));
    }

    [Fact]
    public async Task Content_TheWiFiAnswerIsPublishedAndSearchable()
    {
        SearchContentOutput found = await api.AskAsGuestAsync<SearchContentOutput>(
            Tenant, "Property.SearchContent", new { propertyId = AuroraGrand.PropertyId.Value, query = "wifi password" }, Cancellation);

        Assert.Equal(AuroraGrand.WiFiContent, found.Results[0].Source.Id);
    }

    [Fact]
    public async Task Demo5_AlexIsInHouseInRoom412_LinkedToThePmsReservation()
    {
        await using AsyncServiceScope scope = TenantScope();
        StayContext? stay = await scope.ServiceProvider.GetRequiredService<IStayContextRepository>().FindInHouseInUnitAsync(AuroraGrand.PropertyId, AuroraGrand.Room412, Cancellation);
        Guid? linked = await scope.ServiceProvider.GetRequiredService<IExternalReferences>().FindCanonicalIdAsync(
            AuroraGrand.PropertyId, "MockPms", nameof(StayContext), AuroraGrand.AlexReservation, Cancellation);

        Assert.NotNull(stay);
        Assert.Equal((AuroraGrand.AlexStay, AuroraGrand.Alex, today, today.AddDays(3)), (stay.Id, stay.GuestId, stay.Arrival, stay.Departure));
        Assert.Equal(AuroraGrand.AlexStay.Value, linked);
    }

    [Fact]
    public async Task Demo5_StaffSeeTheServicesForTowelsPillowsAndRepairs()
    {
        using HttpClient worker = api.Staff(Tenant, "Worker", "sam");

        IReadOnlyList<ServiceResponse>? services = await worker.GetFromJsonAsync<IReadOnlyList<ServiceResponse>>(
            $"/api/v1/services?propertyId={AuroraGrand.PropertyId}", ContractJson.Options, Cancellation);

        ServiceResponse amenities = Assert.Single(services!, service => service.Id == AuroraGrand.HousekeepingAmenities);
        Assert.Equal(("housekeeping.amenities", true), (amenities.Code, amenities.IsGuestRequestable));
        Assert.Contains(new ServiceItemResponse("bath_towel", "Bath towel", 4), amenities.Items);
        Assert.Contains(new ServiceItemResponse("pillow", "Pillow", 2), amenities.Items);
        Assert.Contains(services!, service => (service.Id, service.Code, service.Category) == (AuroraGrand.MaintenanceRepair, "maintenance.repair", ServiceCategory.Maintenance));
    }

    [Fact]
    public async Task Seed_IsAuditedAsTheSeed_AndRunningItAgainCreatesNothing()
    {
        IReadOnlyList<AuditRecord> auditBefore = await api.AuditAsync(Tenant, Cancellation);
        int eventsBefore = (await api.EventsAsync(Tenant, Cancellation)).Count;

        SeedReport again;
        await using (AsyncServiceScope scope = TenantScope())
        {
            again = await scope.ServiceProvider.GetRequiredService<IReferenceDataSeeder>().SeedAuroraGrandAsync(Cancellation);
        }

        Assert.Contains(auditBefore, audit => (audit.Action, audit.Outcome, audit.Actor, audit.PropertyId)
            == ("Property.Create", AuditOutcome.Succeeded, new Actor(ActorType.System, "reference-data-seed"), AuroraGrand.PropertyId));
        Assert.Contains(auditBefore, audit => (audit.Action, audit.TargetId) == ("Menu.Create", AuroraGrand.SaffronDinner.ToString()));
        Assert.Equal((0, 47), (again.Created, again.Existing));
        Assert.Equal(StayImportOutcome.Unchanged, Assert.Single(again.StayImport.Imported).Outcome);
        Assert.Equal(auditBefore.Count, (await api.AuditAsync(Tenant, Cancellation)).Count);
        Assert.Equal(eventsBefore, (await api.EventsAsync(Tenant, Cancellation)).Count);
    }

    [Fact]
    public async Task Seeder_OutsideTheAuroraGrandsTenant_Refuses()
    {
        TenantId other = new(Guid.CreateVersion7());
        await using AsyncServiceScope scope = api.Factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().Set(other);

        InvalidOperationException refused = await Assert.ThrowsAsync<InvalidOperationException>(
            () => scope.ServiceProvider.GetRequiredService<IReferenceDataSeeder>().SeedAuroraGrandAsync(Cancellation));

        // Refused before anything is written, not by a later clash with the Aurora Grand's fixed IDs.
        Assert.StartsWith("The Aurora Grand is seeded in its own tenant", refused.Message, StringComparison.Ordinal);
        Assert.Empty(await api.AuditAsync(other, Cancellation));
    }

    [Fact]
    public async Task WithoutTheSetting_TheHostStartsEmpty()
    {
        await using StaffApi plain = new();
        using HttpClient admin = plain.Staff(Tenant);

        using HttpResponseMessage response = await admin.GetAsync($"/api/v1/properties/{AuroraGrand.PropertyId}", Cancellation);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private AsyncServiceScope TenantScope()
    {
        AsyncServiceScope scope = api.Factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().Set(Tenant);
        scope.ServiceProvider.GetRequiredService<OperationContext>().ActAs(new Actor(ActorType.System, "reference-data-seed"));

        return scope;
    }
}
