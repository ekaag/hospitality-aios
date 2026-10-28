using System.Net;
using System.Net.Http.Json;
using HospitalityOS.Contracts.Api.Properties;
using HospitalityOS.Contracts.Api.Restaurants;
using HospitalityOS.Contracts.Api.Spaces;
using HospitalityOS.Contracts.Capabilities.Dining;
using HospitalityOS.Contracts.Serialization;
using HospitalityOS.Ports.Auditing;
using HospitalityOS.SharedKernel.Tenancy;
using Xunit;

namespace HospitalityOS.IntegrationTests.Hosts.Api;

/// <summary>
/// Journey 3 through the real host: an administrator adds a restaurant to the right property (and a space in it),
/// emitting <c>RestaurantCreated</c>, and the concierge finds it; updates emit <c>RestaurantUpdated</c>.
/// </summary>
public sealed class RestaurantEndpointsTests : IAsyncDisposable
{
    private const string Route = "/api/v1/restaurants";

    private readonly StaffApi api = new();
    private readonly TenantId tenant = new(Guid.CreateVersion7());

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    public ValueTask DisposeAsync() => api.DisposeAsync();

    [Fact]
    public async Task Create_LinksTheRestaurantToItsPropertyAndLocation_AndTheConciergeFindsIt()
    {
        PropertyResponse property = await api.CreatePropertyAsync(tenant, Cancellation);
        using HttpClient admin = api.Staff(tenant);
        SpaceResponse lobby = await CreateSpaceAsync(admin, property, "Lobby");

        using HttpResponseMessage response = await admin.PostAsJsonAsync(
            Route, new { propertyId = property.Id.Value, name = " Harbour Grill ", cuisine = "Seafood", locationSpaceId = lobby.Id.Value }, Cancellation);

        RestaurantResponse grill = await StaffApi.CreatedAsync<RestaurantResponse>(response, Cancellation);
        Assert.Equal((property.Id, "Harbour Grill", "Seafood", lobby.Id, 1L), (grill.PropertyId, grill.Name, grill.Cuisine, grill.LocationSpaceId, grill.Version));
        Assert.Equal($"{Route}/{grill.Id}", response.Headers.Location?.OriginalString);
        Assert.Single(await api.EventsAsync(tenant, Cancellation), message => message.EventType == "RestaurantCreated" && message.AggregateId == grill.Id);
        Assert.Contains(await api.AuditAsync(tenant, Cancellation), audit => (audit.Action, audit.TargetId, audit.PropertyId) == ("Restaurant.Create", grill.Id.ToString(), property.Id));

        SearchRestaurantsOutput found = await api.AskAsGuestAsync<SearchRestaurantsOutput>(tenant, "Dining.SearchRestaurants", new { propertyId = property.Id.Value, query = "seafood" }, Cancellation);
        RestaurantEntry entry = Assert.Single(found.Restaurants);
        Assert.Equal((grill.Id, 1L, lobby.Id), (entry.Source.Id, entry.Source.Version, entry.LocationSpaceId));
    }

    [Fact]
    public async Task Update_ChangesTheRestaurant_ClearsOmittedFields_AndEmitsRestaurantUpdated()
    {
        PropertyResponse property = await api.CreatePropertyAsync(tenant, Cancellation);
        using HttpClient admin = api.Staff(tenant);
        RestaurantResponse grill = await CreateAsync(admin, property, await CreateSpaceAsync(admin, property, "Lobby"));

        using HttpResponseMessage response = await StaffApi.SendAsync(
            admin, HttpMethod.Put, $"{Route}/{grill.Id}", new { name = "Harbour Grill & Bar", description = "Open-fire seafood" }, "\"1\"", Cancellation);

        RestaurantResponse updated = await StaffApi.OkAsync<RestaurantResponse>(response, Cancellation);
        Assert.Equal(("Harbour Grill & Bar", null, "Open-fire seafood", null, 2L), (updated.Name, updated.Cuisine, updated.Description, updated.LocationSpaceId, updated.Version));
        Assert.Equal("\"2\"", response.Headers.ETag?.Tag);
        Assert.Single(await api.EventsAsync(tenant, Cancellation), message => message.EventType == "RestaurantUpdated");
    }

    [Fact]
    public async Task Update_FromAnOldVersion_Is412_AndChangesNothing()
    {
        PropertyResponse property = await api.CreatePropertyAsync(tenant, Cancellation);
        using HttpClient admin = api.Staff(tenant);
        RestaurantResponse grill = await CreateAsync(admin, property, null);
        using HttpResponseMessage first = await StaffApi.SendAsync(admin, HttpMethod.Put, $"{Route}/{grill.Id}", new { name = "Grill" }, "\"1\"", Cancellation);

        using HttpResponseMessage stale = await StaffApi.SendAsync(admin, HttpMethod.Put, $"{Route}/{grill.Id}", new { name = "Bar" }, "\"1\"", Cancellation);

        Assert.Equal((HttpStatusCode.PreconditionFailed, "request.version_mismatch"), await StaffApi.ProblemAsync(stale, Cancellation));
        RestaurantResponse? current = await admin.GetFromJsonAsync<RestaurantResponse>($"{Route}/{grill.Id}", ContractJson.Options, Cancellation);
        Assert.Equal(("Grill", 2L), (current!.Name, current.Version));
    }

    [Fact]
    public async Task Create_InAnotherPropertysSpace_WithoutAName_ReportsBothFields_AndIsAudited()
    {
        PropertyResponse grand = await api.CreatePropertyAsync(tenant, Cancellation);
        PropertyResponse bay = await api.CreatePropertyAsync(tenant, Cancellation, "Aurora Bay");
        using HttpClient admin = api.Staff(tenant);
        SpaceResponse grandLobby = await CreateSpaceAsync(admin, grand, "Lobby");

        using HttpResponseMessage response = await admin.PostAsJsonAsync(Route, new { propertyId = bay.Id.Value, locationSpaceId = grandLobby.Id.Value }, Cancellation);

        Assert.Equal(["restaurant.name_required", "restaurant.location_not_found"], await StaffApi.FieldCodesAsync(response, Cancellation));
        Assert.DoesNotContain(await api.EventsAsync(tenant, Cancellation), message => message.EventType == "RestaurantCreated");
        Assert.Contains(await api.AuditAsync(tenant, Cancellation), audit => (audit.Action, audit.Outcome) == ("Restaurant.Create", AuditOutcome.Failed));
    }

    [Fact]
    public async Task List_ReturnsOnlyThePropertysRestaurants_ByName_ToAnyStaffMember()
    {
        PropertyResponse grand = await api.CreatePropertyAsync(tenant, Cancellation);
        PropertyResponse bay = await api.CreatePropertyAsync(tenant, Cancellation, "Aurora Bay");
        using HttpClient admin = api.Staff(tenant);
        await CreateAsync(admin, grand, null, "Sky Bar");
        await CreateAsync(admin, grand, null, "harbour Grill");
        await CreateAsync(admin, bay, null, "Bay Café");
        using HttpClient worker = api.Staff(tenant, "Worker", "sam");

        IReadOnlyList<RestaurantResponse>? listed = await worker.GetFromJsonAsync<IReadOnlyList<RestaurantResponse>>($"{Route}?propertyId={grand.Id}", ContractJson.Options, Cancellation);

        Assert.Equal(["harbour Grill", "Sky Bar"], listed!.Select(restaurant => restaurant.Name));
    }

    [Fact]
    public async Task Create_AsManager_Is403_AndAuditedAsDenied()
    {
        PropertyResponse property = await api.CreatePropertyAsync(tenant, Cancellation);
        using HttpClient manager = api.Staff(tenant, "Manager", "lee");

        using HttpResponseMessage response = await manager.PostAsJsonAsync(Route, new { propertyId = property.Id.Value, name = "Grill" }, Cancellation);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Contains(await api.AuditAsync(tenant, Cancellation), audit => (audit.Action, audit.Outcome, audit.Actor.Id) == ("Restaurant.Create", AuditOutcome.Denied, "lee"));
    }

    private static async Task<RestaurantResponse> CreateAsync(HttpClient admin, PropertyResponse property, SpaceResponse? location, string name = "Harbour Grill")
    {
        using HttpResponseMessage response = await admin.PostAsJsonAsync(
            Route, new { propertyId = property.Id.Value, name, cuisine = "Seafood", locationSpaceId = location?.Id.Value }, Cancellation);

        return await StaffApi.CreatedAsync<RestaurantResponse>(response, Cancellation);
    }

    private static async Task<SpaceResponse> CreateSpaceAsync(HttpClient admin, PropertyResponse property, string name)
    {
        using HttpResponseMessage response = await admin.PostAsJsonAsync("/api/v1/spaces", new { propertyId = property.Id.Value, name, kind = "Area" }, Cancellation);

        return await StaffApi.CreatedAsync<SpaceResponse>(response, Cancellation);
    }
}
