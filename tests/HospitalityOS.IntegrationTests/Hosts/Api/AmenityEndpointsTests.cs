using System.Net;
using System.Net.Http.Json;
using HospitalityOS.Contracts.Api.Amenities;
using HospitalityOS.Contracts.Api.Properties;
using HospitalityOS.Contracts.Api.Spaces;
using HospitalityOS.Contracts.Capabilities.Properties;
using HospitalityOS.Contracts.Serialization;
using HospitalityOS.Ports.Auditing;
using HospitalityOS.SharedKernel.Tenancy;
using Xunit;

namespace HospitalityOS.IntegrationTests.Hosts.Api;

/// <summary>
/// Journey 2 through the real host: an administrator creates an amenity linked to the right property (and a space in
/// it), emitting <c>AmenityCreated</c>; updates emit <c>AmenityUpdated</c>; a rejected update changes nothing.
/// </summary>
public sealed class AmenityEndpointsTests : IAsyncDisposable
{
    private const string Route = "/api/v1/amenities";

    private readonly StaffApi api = new();
    private readonly TenantId tenant = new(Guid.CreateVersion7());

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    public ValueTask DisposeAsync() => api.DisposeAsync();

    [Fact]
    public async Task Create_LinksTheAmenityToItsPropertyAndLocation_AndEmitsAmenityCreated()
    {
        PropertyResponse property = await api.CreatePropertyAsync(tenant, Cancellation);
        using HttpClient admin = api.Staff(tenant);
        SpaceResponse deck = await CreateSpaceAsync(admin, property, "Pool Deck");

        using HttpResponseMessage response = await admin.PostAsJsonAsync(
            Route, new { propertyId = property.Id.Value, name = "Rooftop Pool", category = "Pool", locationSpaceId = deck.Id.Value }, Cancellation);

        AmenityResponse pool = await StaffApi.CreatedAsync<AmenityResponse>(response, Cancellation);
        Assert.Equal((property.Id, "Rooftop Pool", AmenityCategory.Pool, deck.Id, 1L), (pool.PropertyId, pool.Name, pool.Category, pool.LocationSpaceId, pool.Version));
        Assert.Equal($"{Route}/{pool.Id}", response.Headers.Location?.OriginalString);
        Assert.Single(await api.EventsAsync(tenant, Cancellation), message => message.EventType == "AmenityCreated" && message.AggregateId == pool.Id);
        Assert.Contains(await api.AuditAsync(tenant, Cancellation), audit => (audit.Action, audit.TargetId, audit.PropertyId) == ("Amenity.Create", pool.Id.ToString(), property.Id));
    }

    [Fact]
    public async Task Update_ChangesTheAmenity_ClearsAnOmittedLocation_AndEmitsAmenityUpdated()
    {
        PropertyResponse property = await api.CreatePropertyAsync(tenant, Cancellation);
        using HttpClient admin = api.Staff(tenant);
        AmenityResponse gym = await CreateAsync(admin, property, await CreateSpaceAsync(admin, property, "Level 2"));

        using HttpResponseMessage response = await StaffApi.SendAsync(
            admin, HttpMethod.Put, $"{Route}/{gym.Id}", new { name = "Fitness Center", category = "Fitness", description = "Open to guests 16+" }, "\"1\"", Cancellation);

        AmenityResponse updated = await StaffApi.OkAsync<AmenityResponse>(response, Cancellation);
        Assert.Equal(("Fitness Center", "Open to guests 16+", null, 2L), (updated.Name, updated.Description, updated.LocationSpaceId, updated.Version));
        Assert.Single(await api.EventsAsync(tenant, Cancellation), message => message.EventType == "AmenityUpdated");
    }

    [Fact]
    public async Task Update_WithAnUnknownLocationAndNoCategory_IsRejected_AndChangesNothing()
    {
        PropertyResponse property = await api.CreatePropertyAsync(tenant, Cancellation);
        using HttpClient admin = api.Staff(tenant);
        SpaceResponse level2 = await CreateSpaceAsync(admin, property, "Level 2");
        AmenityResponse gym = await CreateAsync(admin, property, level2);

        using HttpResponseMessage response = await admin.PutAsJsonAsync($"{Route}/{gym.Id}", new { name = "Gym", locationSpaceId = Guid.CreateVersion7() }, Cancellation);

        Assert.Equal(["amenity.location_not_found", "amenity.category_required"], await StaffApi.FieldCodesAsync(response, Cancellation));
        AmenityResponse? unchanged = await admin.GetFromJsonAsync<AmenityResponse>($"{Route}/{gym.Id}", ContractJson.Options, Cancellation);
        Assert.Equal(gym, unchanged);
        Assert.DoesNotContain(await api.EventsAsync(tenant, Cancellation), message => message.EventType == "AmenityUpdated");
        Assert.Contains(await api.AuditAsync(tenant, Cancellation), audit => (audit.Action, audit.Outcome, audit.PropertyId) == ("Amenity.Update", AuditOutcome.Failed, property.Id));
    }

    [Fact]
    public async Task Create_InAnotherPropertysSpace_IsALocationError()
    {
        PropertyResponse grand = await api.CreatePropertyAsync(tenant, Cancellation);
        PropertyResponse bay = await api.CreatePropertyAsync(tenant, Cancellation, "Aurora Bay");
        using HttpClient admin = api.Staff(tenant);
        SpaceResponse grandDeck = await CreateSpaceAsync(admin, grand, "Pool Deck");

        using HttpResponseMessage response = await admin.PostAsJsonAsync(
            Route, new { propertyId = bay.Id.Value, name = "Pool", category = "Pool", locationSpaceId = grandDeck.Id.Value }, Cancellation);

        Assert.Equal(["amenity.location_other_property"], await StaffApi.FieldCodesAsync(response, Cancellation));
    }

    [Fact]
    public async Task Create_AsManager_Is403_AndAuditedAsDenied()
    {
        PropertyResponse property = await api.CreatePropertyAsync(tenant, Cancellation);
        using HttpClient manager = api.Staff(tenant, "Manager", "lee");

        using HttpResponseMessage response = await manager.PostAsJsonAsync(Route, new { propertyId = property.Id.Value, name = "Pool", category = "Pool" }, Cancellation);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Contains(await api.AuditAsync(tenant, Cancellation), audit => (audit.Action, audit.Outcome, audit.Actor.Id) == ("Amenity.Create", AuditOutcome.Denied, "lee"));
    }

    private static async Task<AmenityResponse> CreateAsync(HttpClient admin, PropertyResponse property, SpaceResponse location)
    {
        using HttpResponseMessage response = await admin.PostAsJsonAsync(
            Route, new { propertyId = property.Id.Value, name = "Gym", category = "Fitness", locationSpaceId = location.Id.Value }, Cancellation);

        return await StaffApi.CreatedAsync<AmenityResponse>(response, Cancellation);
    }

    private static async Task<SpaceResponse> CreateSpaceAsync(HttpClient admin, PropertyResponse property, string name)
    {
        using HttpResponseMessage response = await admin.PostAsJsonAsync("/api/v1/spaces", new { propertyId = property.Id.Value, name, kind = "Area" }, Cancellation);

        return await StaffApi.CreatedAsync<SpaceResponse>(response, Cancellation);
    }
}
