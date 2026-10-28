using System.Net;
using System.Net.Http.Json;
using HospitalityOS.Contracts.Api.Properties;
using HospitalityOS.Contracts.Api.Spaces;
using HospitalityOS.Contracts.Serialization;
using HospitalityOS.Ports.Auditing;
using HospitalityOS.SharedKernel.Tenancy;
using Xunit;

namespace HospitalityOS.IntegrationTests.Hosts.Api;

/// <summary>
/// <c>/api/v1/spaces</c> through the real host: an administrator builds a property's hierarchy of places; parents must
/// be in the same property; changes are versioned and audited; other tenants see nothing.
/// </summary>
public sealed class SpaceEndpointsTests : IAsyncDisposable
{
    private const string Route = "/api/v1/spaces";

    private readonly StaffApi api = new();
    private readonly TenantId tenant = new(Guid.CreateVersion7());

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    public ValueTask DisposeAsync() => api.DisposeAsync();

    [Fact]
    public async Task Create_BuildsAHierarchy_EmitsSpaceCreated_AndAuditsTheProperty()
    {
        PropertyResponse property = await api.CreatePropertyAsync(tenant, Cancellation);
        using HttpClient admin = api.Staff(tenant);

        SpaceResponse building = await CreateAsync(admin, new { propertyId = property.Id.Value, name = "Main Building", kind = "Building" });
        SpaceResponse floor = await CreateAsync(admin, new { propertyId = property.Id.Value, parentSpaceId = building.Id.Value, name = " Floor 4 ", kind = "Floor", code = "4" });

        Assert.Equal((building.Id, "Floor 4", SpaceKind.Floor, "4", 1L), (floor.ParentSpaceId, floor.Name, floor.Kind, floor.Code, floor.Version));
        Assert.Equal(2, (await api.EventsAsync(tenant, Cancellation)).Count(message => message.EventType == "SpaceCreated"));
        AuditRecord audit = (await api.AuditAsync(tenant, Cancellation))[^1];
        Assert.Equal(("Space.Create", AuditOutcome.Succeeded, "Space", floor.Id.ToString(), property.Id), (audit.Action, audit.Outcome, audit.TargetType, audit.TargetId, audit.PropertyId));
    }

    [Fact]
    public async Task Create_WithMissingFieldsAndUnknownParent_ReportsEveryField_AndSavesNothing()
    {
        PropertyResponse property = await api.CreatePropertyAsync(tenant, Cancellation);
        using HttpClient admin = api.Staff(tenant);

        using HttpResponseMessage response = await admin.PostAsJsonAsync(Route, new { propertyId = property.Id.Value, parentSpaceId = Guid.CreateVersion7() }, Cancellation);

        Assert.Equal(["space.name_required", "space.parent_not_found", "space.kind_required"], await StaffApi.FieldCodesAsync(response, Cancellation));
        Assert.Empty(await ListAsync(admin, property));
        Assert.DoesNotContain(await api.EventsAsync(tenant, Cancellation), message => message.EventType == "SpaceCreated");
    }

    [Fact]
    public async Task Create_InsideAnotherPropertysSpace_OrForAnUnknownProperty_IsAFieldError()
    {
        PropertyResponse grand = await api.CreatePropertyAsync(tenant, Cancellation);
        PropertyResponse bay = await api.CreatePropertyAsync(tenant, Cancellation, "Aurora Bay");
        using HttpClient admin = api.Staff(tenant);
        SpaceResponse grandLobby = await CreateAsync(admin, new { propertyId = grand.Id.Value, name = "Lobby", kind = "Area" });

        using HttpResponseMessage otherProperty = await admin.PostAsJsonAsync(Route, new { propertyId = bay.Id.Value, parentSpaceId = grandLobby.Id.Value, name = "Desk", kind = "Area" }, Cancellation);
        using HttpResponseMessage unknownProperty = await admin.PostAsJsonAsync(Route, new { propertyId = Guid.CreateVersion7(), name = "Desk", kind = "Area" }, Cancellation);

        Assert.Equal(["space.parent_other_property"], await StaffApi.FieldCodesAsync(otherProperty, Cancellation));
        Assert.Equal(["space.property_not_found"], await StaffApi.FieldCodesAsync(unknownProperty, Cancellation));
    }

    [Fact]
    public async Task Update_WithTheCurrentVersion_EmitsSpaceUpdated_ButAStaleOneIs412()
    {
        PropertyResponse property = await api.CreatePropertyAsync(tenant, Cancellation);
        using HttpClient admin = api.Staff(tenant);
        SpaceResponse pool = await CreateAsync(admin, new { propertyId = property.Id.Value, name = "Pool", kind = "Area" });

        using HttpResponseMessage updated = await StaffApi.SendAsync(admin, HttpMethod.Put, $"{Route}/{pool.Id}", new { name = "Pool Deck", kind = "Outdoor", directions = "Level 3" }, "\"1\"", Cancellation);
        using HttpResponseMessage stale = await StaffApi.SendAsync(admin, HttpMethod.Put, $"{Route}/{pool.Id}", new { name = "Pool", kind = "Area" }, "\"1\"", Cancellation);

        SpaceResponse current = await StaffApi.OkAsync<SpaceResponse>(updated, Cancellation);
        Assert.Equal(("Pool Deck", SpaceKind.Outdoor, "Level 3", 2L, "\"2\""), (current.Name, current.Kind, current.Directions, current.Version, updated.Headers.ETag?.Tag));
        Assert.Equal(HttpStatusCode.PreconditionFailed, stale.StatusCode);
        Assert.Single(await api.EventsAsync(tenant, Cancellation), message => message.EventType == "SpaceUpdated");
    }

    [Fact]
    public async Task AnotherTenant_CanNeitherReadNorChangeNorListTheSpaces()
    {
        PropertyResponse property = await api.CreatePropertyAsync(tenant, Cancellation);
        using HttpClient owner = api.Staff(tenant);
        SpaceResponse lobby = await CreateAsync(owner, new { propertyId = property.Id.Value, name = "Lobby", kind = "Area" });
        using HttpClient intruder = api.Staff(new TenantId(Guid.CreateVersion7()), "Admin", "mallory");

        using HttpResponseMessage read = await intruder.GetAsync($"{Route}/{lobby.Id}", Cancellation);
        using HttpResponseMessage change = await intruder.PutAsJsonAsync($"{Route}/{lobby.Id}", new { name = "Mine", kind = "Area" }, Cancellation);
        using HttpResponseMessage create = await intruder.PostAsJsonAsync(Route, new { propertyId = property.Id.Value, name = "Mine", kind = "Area" }, Cancellation);

        Assert.Equal((HttpStatusCode.NotFound, HttpStatusCode.NotFound), (read.StatusCode, change.StatusCode));
        Assert.Equal(["space.property_not_found"], await StaffApi.FieldCodesAsync(create, Cancellation));
        Assert.Empty(await ListAsync(intruder, property));
        Assert.Equal(["Lobby"], (await ListAsync(owner, property)).Select(space => space.Name));
    }

    [Fact]
    public async Task List_ReturnsOnlyThatPropertysSpaces_ByName()
    {
        PropertyResponse grand = await api.CreatePropertyAsync(tenant, Cancellation);
        PropertyResponse bay = await api.CreatePropertyAsync(tenant, Cancellation, "Aurora Bay");
        using HttpClient admin = api.Staff(tenant);
        await CreateAsync(admin, new { propertyId = grand.Id.Value, name = "Spa", kind = "Room" });
        await CreateAsync(admin, new { propertyId = grand.Id.Value, name = "gym", kind = "Room" });
        await CreateAsync(admin, new { propertyId = bay.Id.Value, name = "Beach", kind = "Outdoor" });
        using HttpClient worker = api.Staff(tenant, "Worker", "sam");

        Assert.Equal(["gym", "Spa"], (await ListAsync(worker, grand)).Select(space => space.Name));
    }

    private static async Task<SpaceResponse> CreateAsync(HttpClient admin, object body)
    {
        using HttpResponseMessage response = await admin.PostAsJsonAsync(Route, body, Cancellation);

        return await StaffApi.CreatedAsync<SpaceResponse>(response, Cancellation);
    }

    private static async Task<List<SpaceResponse>> ListAsync(HttpClient client, PropertyResponse property) =>
        await client.GetFromJsonAsync<List<SpaceResponse>>($"{Route}?propertyId={property.Id}", ContractJson.Options, Cancellation) ?? [];
}
