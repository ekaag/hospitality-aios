using System.Net;
using System.Net.Http.Json;
using HospitalityOS.Contracts.Api.Properties;
using HospitalityOS.Contracts.Api.Staff;
using HospitalityOS.Contracts.Serialization;
using HospitalityOS.SharedKernel.Ids;
using HospitalityOS.SharedKernel.Tenancy;
using Xunit;

namespace HospitalityOS.IntegrationTests.Hosts.Api;

/// <summary>
/// The roster through the real host: administrators add and update staff, under the ID their token names, emitting
/// <c>StaffMemberCreated</c> and <c>StaffMemberUpdated</c>; a worker puts only themselves on or off duty, a manager
/// anyone, emitting <c>StaffMemberDutyChanged</c>; and another tenant's staff do not exist.
/// </summary>
public sealed class StaffEndpointsTests : IAsyncDisposable
{
    private const string Route = "/api/v1/staff";

    private readonly StaffApi api = new();
    private readonly TenantId tenant = new(Guid.CreateVersion7());

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    public ValueTask DisposeAsync() => api.DisposeAsync();

    [Fact]
    public async Task Create_UnderTheGivenId_ListsByName_AndEmitsStaffMemberCreated()
    {
        PropertyResponse property = await api.CreatePropertyAsync(tenant, Cancellation);
        using HttpClient admin = api.Staff(tenant);
        StaffMemberId samId = new(Guid.CreateVersion7());

        StaffMemberResponse sam = await CreateAsync(admin, new CreateStaffMemberRequest(samId, property.Id, " Sam Patel ", ["Housekeeping"], true));
        StaffMemberResponse jo = await CreateAsync(admin, new CreateStaffMemberRequest(PropertyId: property.Id, DisplayName: "Jo Okafor"));

        Assert.Equal((samId, "Sam Patel", true, 1L), (sam.Id, sam.DisplayName, sam.IsOnDuty, sam.Version));
        Assert.Equal(["housekeeping"], sam.Skills);
        Assert.Equal((false, 0), (jo.IsOnDuty, jo.Skills.Count));
        Assert.Equal(
            ["Jo Okafor", "Sam Patel"],
            (await admin.GetFromJsonAsync<List<StaffMemberResponse>>($"{Route}?propertyId={property.Id}", ContractJson.Options, Cancellation))!.Select(member => member.DisplayName));
        Assert.Single(await api.EventsAsync(tenant, Cancellation), message => message.EventType == "StaffMemberCreated" && message.AggregateId == samId.Value);
    }

    [Fact]
    public async Task Create_WithATakenIdNoNameOrAnUnknownProperty_IsRejected_AndOnlyAdministratorsMayCreate()
    {
        PropertyResponse property = await api.CreatePropertyAsync(tenant, Cancellation);
        using HttpClient admin = api.Staff(tenant);
        StaffMemberResponse sam = await CreateAsync(admin, new CreateStaffMemberRequest(PropertyId: property.Id, DisplayName: "Sam Patel"));
        using HttpClient manager = api.Staff(tenant, "Manager");

        using HttpResponseMessage taken = await admin.PostAsJsonAsync(Route, new CreateStaffMemberRequest(sam.Id, property.Id, "Sam Again"), ContractJson.Options, Cancellation);
        using HttpResponseMessage nameless = await admin.PostAsJsonAsync(Route, new CreateStaffMemberRequest(PropertyId: property.Id), ContractJson.Options, Cancellation);
        using HttpResponseMessage nowhere = await admin.PostAsJsonAsync(Route, new CreateStaffMemberRequest(PropertyId: new PropertyId(Guid.CreateVersion7()), DisplayName: "Jo"), ContractJson.Options, Cancellation);
        using HttpResponseMessage denied = await manager.PostAsJsonAsync(Route, new CreateStaffMemberRequest(PropertyId: property.Id, DisplayName: "Jo"), ContractJson.Options, Cancellation);

        Assert.Equal(["staff_member.id_taken"], await StaffApi.FieldCodesAsync(taken, Cancellation));
        Assert.Equal(["staff_member.display_name_required"], await StaffApi.FieldCodesAsync(nameless, Cancellation));
        Assert.Equal(["staff_member.property_not_found"], await StaffApi.FieldCodesAsync(nowhere, Cancellation));
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
    }

    [Fact]
    public async Task Update_ReplacesNameAndSkills_AndEmitsStaffMemberUpdated()
    {
        PropertyResponse property = await api.CreatePropertyAsync(tenant, Cancellation);
        using HttpClient admin = api.Staff(tenant);
        StaffMemberResponse sam = await CreateAsync(admin, new CreateStaffMemberRequest(PropertyId: property.Id, DisplayName: "Sam Patel", Skills: ["housekeeping"]));

        using HttpResponseMessage response = await StaffApi.SendAsync(
            admin, HttpMethod.Put, $"{Route}/{sam.Id}", new UpdateStaffMemberRequest("Sam Patel", ["housekeeping", "minibar"]), "\"1\"", Cancellation);

        StaffMemberResponse updated = await StaffApi.OkAsync<StaffMemberResponse>(response, Cancellation);
        Assert.Equal(["housekeeping", "minibar"], updated.Skills);
        Assert.Single(await api.EventsAsync(tenant, Cancellation), message => message.EventType == "StaffMemberUpdated");
    }

    [Fact]
    public async Task Duty_AWorkerSetsOnlyTheirOwn_AManagerSetsAnyones()
    {
        PropertyResponse property = await api.CreatePropertyAsync(tenant, Cancellation);
        using HttpClient admin = api.Staff(tenant);
        StaffMemberResponse sam = await CreateAsync(admin, new CreateStaffMemberRequest(PropertyId: property.Id, DisplayName: "Sam Patel"));
        StaffMemberResponse jo = await CreateAsync(admin, new CreateStaffMemberRequest(PropertyId: property.Id, DisplayName: "Jo Okafor"));
        using HttpClient samClient = api.Staff(tenant, "Worker", sam.Id.ToString());
        using HttpClient manager = api.Staff(tenant, "Manager");

        using HttpResponseMessage own = await samClient.PutAsJsonAsync($"{Route}/{sam.Id}/duty", new SetDutyRequest(true), Cancellation);
        using HttpResponseMessage others = await samClient.PutAsJsonAsync($"{Route}/{jo.Id}/duty", new SetDutyRequest(true), Cancellation);
        using HttpResponseMessage managed = await manager.PutAsJsonAsync($"{Route}/{jo.Id}/duty", new SetDutyRequest(true), Cancellation);
        using HttpResponseMessage missing = await manager.PutAsJsonAsync($"{Route}/{jo.Id}/duty", new SetDutyRequest(), Cancellation);

        Assert.True((await StaffApi.OkAsync<StaffMemberResponse>(own, Cancellation)).IsOnDuty);
        Assert.Equal((HttpStatusCode.Forbidden, "staff_member.duty_forbidden"), await StaffApi.ProblemAsync(others, Cancellation));
        Assert.True((await StaffApi.OkAsync<StaffMemberResponse>(managed, Cancellation)).IsOnDuty);
        Assert.Equal(["staff_member.is_on_duty_required"], await StaffApi.FieldCodesAsync(missing, Cancellation));
        Assert.Equal(2, (await api.EventsAsync(tenant, Cancellation)).Count(message => message.EventType == "StaffMemberDutyChanged"));
    }

    [Fact]
    public async Task AnotherTenantsStaff_DoNotExist()
    {
        PropertyResponse property = await api.CreatePropertyAsync(tenant, Cancellation);
        using HttpClient admin = api.Staff(tenant);
        StaffMemberResponse sam = await CreateAsync(admin, new CreateStaffMemberRequest(PropertyId: property.Id, DisplayName: "Sam Patel"));
        using HttpClient stranger = api.Staff(new TenantId(Guid.CreateVersion7()));

        using HttpResponseMessage response = await stranger.GetAsync($"{Route}/{sam.Id}", Cancellation);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private static async Task<StaffMemberResponse> CreateAsync(HttpClient admin, CreateStaffMemberRequest request)
    {
        using HttpResponseMessage response = await admin.PostAsJsonAsync(Route, request, ContractJson.Options, Cancellation);

        return await StaffApi.CreatedAsync<StaffMemberResponse>(response, Cancellation);
    }
}
