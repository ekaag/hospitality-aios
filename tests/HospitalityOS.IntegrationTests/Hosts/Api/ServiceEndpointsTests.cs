using System.Net;
using System.Net.Http.Json;
using HospitalityOS.Contracts.Api.Properties;
using HospitalityOS.Contracts.Api.Services;
using HospitalityOS.Contracts.Serialization;
using HospitalityOS.Ports.Auditing;
using HospitalityOS.SharedKernel.Tenancy;
using Xunit;

namespace HospitalityOS.IntegrationTests.Hosts.Api;

/// <summary>
/// Services through the real host: an administrator defines what guests can request, with codes unique within a
/// property, emitting <c>ServiceCreated</c> and <c>ServiceUpdated</c>.
/// </summary>
public sealed class ServiceEndpointsTests : IAsyncDisposable
{
    private const string Route = "/api/v1/services";

    private readonly StaffApi api = new();
    private readonly TenantId tenant = new(Guid.CreateVersion7());

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    public ValueTask DisposeAsync() => api.DisposeAsync();

    [Fact]
    public async Task Create_DefinesTheServiceWithItsItems_AndEmitsServiceCreated()
    {
        PropertyResponse property = await api.CreatePropertyAsync(tenant, Cancellation);
        using HttpClient admin = api.Staff(tenant);

        using HttpResponseMessage response = await admin.PostAsJsonAsync(
            Route,
            new
            {
                propertyId = property.Id.Value,
                code = " Housekeeping.Amenities ",
                name = "Housekeeping amenities",
                category = "Housekeeping",
                items = new[] { new { code = "Bath_Towel", name = "Bath towel", maxQuantityPerRequest = 4 } },
                targetCompletion = "00:30:00",
            },
            Cancellation);

        ServiceResponse towels = await StaffApi.CreatedAsync<ServiceResponse>(response, Cancellation);
        Assert.Equal(
            (property.Id, "housekeeping.amenities", ServiceCategory.Housekeeping, true, TimeSpan.FromMinutes(30), 1L),
            (towels.PropertyId, towels.Code, towels.Category, towels.IsGuestRequestable, towels.TargetCompletion, towels.Version));
        Assert.Equal([new ServiceItemResponse("bath_towel", "Bath towel", 4)], towels.Items);
        Assert.Single(await api.EventsAsync(tenant, Cancellation), message => message.EventType == "ServiceCreated" && message.AggregateId == towels.Id);
        Assert.Contains(await api.AuditAsync(tenant, Cancellation), audit => (audit.Action, audit.TargetId, audit.PropertyId) == ("Service.Create", towels.Id.ToString(), property.Id));
    }

    [Fact]
    public async Task Create_WithACodeThePropertyAlreadyUses_IsACodeError_ButAnotherPropertyMayUseIt()
    {
        PropertyResponse grand = await api.CreatePropertyAsync(tenant, Cancellation);
        PropertyResponse bay = await api.CreatePropertyAsync(tenant, Cancellation, "Aurora Bay");
        using HttpClient admin = api.Staff(tenant);
        await CreateAsync(admin, grand, "maintenance.repair");

        using HttpResponseMessage duplicate = await admin.PostAsJsonAsync(Route, Definition(grand, "MAINTENANCE.REPAIR"), Cancellation);
        using HttpResponseMessage elsewhere = await admin.PostAsJsonAsync(Route, Definition(bay, "maintenance.repair"), Cancellation);

        Assert.Equal(["service.code_taken"], await StaffApi.FieldCodesAsync(duplicate, Cancellation));
        await StaffApi.CreatedAsync<ServiceResponse>(elsewhere, Cancellation);
    }

    [Fact]
    public async Task Update_KeepingItsOwnCode_Succeeds_ButTakingAnothersIsRejected_AndChangesNothing()
    {
        PropertyResponse property = await api.CreatePropertyAsync(tenant, Cancellation);
        using HttpClient admin = api.Staff(tenant);
        ServiceResponse repair = await CreateAsync(admin, property, "maintenance.repair");
        await CreateAsync(admin, property, "transport.airport");

        using HttpResponseMessage renamed = await StaffApi.SendAsync(
            admin, HttpMethod.Put, $"{Route}/{repair.Id}", new { code = "maintenance.repair", name = "Repairs", category = "Maintenance", isGuestRequestable = false }, "\"1\"", Cancellation);
        using HttpResponseMessage taken = await admin.PutAsJsonAsync($"{Route}/{repair.Id}", new { code = "transport.airport", name = "Repairs", category = "Maintenance" }, Cancellation);

        ServiceResponse updated = await StaffApi.OkAsync<ServiceResponse>(renamed, Cancellation);
        Assert.Equal(("Repairs", false, 2L), (updated.Name, updated.IsGuestRequestable, updated.Version));
        Assert.Equal(["service.code_taken"], await StaffApi.FieldCodesAsync(taken, Cancellation));
        ServiceResponse? current = await admin.GetFromJsonAsync<ServiceResponse>($"{Route}/{repair.Id}", ContractJson.Options, Cancellation);
        Assert.Equal(updated, current);
        Assert.Single(await api.EventsAsync(tenant, Cancellation), message => message.EventType == "ServiceUpdated");
    }

    [Fact]
    public async Task Create_WithIncompleteItems_ReportsThemByPosition()
    {
        PropertyResponse property = await api.CreatePropertyAsync(tenant, Cancellation);
        using HttpClient admin = api.Staff(tenant);

        using HttpResponseMessage response = await admin.PostAsJsonAsync(
            Route,
            new
            {
                propertyId = property.Id.Value,
                code = "housekeeping.amenities",
                name = "Amenities",
                items = new object[] { new { code = "pillow", name = "Pillow" }, new { code = "towel", name = "Towel", maxQuantityPerRequest = 500 } },
            },
            Cancellation);

        Assert.Equal(
            ["service.item_max_quantity_out_of_range", "service.category_required", "service.item_max_quantity_required"],
            await StaffApi.FieldCodesAsync(response, Cancellation));
    }

    [Fact]
    public async Task List_ReturnsThePropertysServices_ByName_AndManagersCannotDefineThem()
    {
        PropertyResponse property = await api.CreatePropertyAsync(tenant, Cancellation);
        using HttpClient admin = api.Staff(tenant);
        await CreateAsync(admin, property, "transport.airport", "Airport transfer");
        await CreateAsync(admin, property, "maintenance.repair", "AC and repairs");
        using HttpClient manager = api.Staff(tenant, "Manager", "lee");

        IReadOnlyList<ServiceResponse>? listed = await manager.GetFromJsonAsync<IReadOnlyList<ServiceResponse>>($"{Route}?propertyId={property.Id}", ContractJson.Options, Cancellation);
        using HttpResponseMessage denied = await manager.PostAsJsonAsync(Route, Definition(property, "concierge.tickets"), Cancellation);

        Assert.Equal(["AC and repairs", "Airport transfer"], listed!.Select(service => service.Name));
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        Assert.Contains(await api.AuditAsync(tenant, Cancellation), audit => (audit.Action, audit.Outcome, audit.Actor.Id) == ("Service.Create", AuditOutcome.Denied, "lee"));
    }

    private static object Definition(PropertyResponse property, string code, string name = "Repairs") =>
        new { propertyId = property.Id.Value, code, name, category = "Maintenance" };

    private static async Task<ServiceResponse> CreateAsync(HttpClient admin, PropertyResponse property, string code, string name = "Repairs")
    {
        using HttpResponseMessage response = await admin.PostAsJsonAsync(Route, Definition(property, code, name), Cancellation);

        return await StaffApi.CreatedAsync<ServiceResponse>(response, Cancellation);
    }
}
