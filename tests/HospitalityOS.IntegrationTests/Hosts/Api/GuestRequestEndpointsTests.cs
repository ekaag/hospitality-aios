using System.Net;
using HospitalityOS.Contracts.Api.ServiceRequests;
using HospitalityOS.SharedKernel.Ids;
using HospitalityOS.Workflows.Routing;
using Xunit;
using static HospitalityOS.IntegrationTests.Workflows.GuestChat;
using Seed = HospitalityOS.Seeding.AuroraGrand;

namespace HospitalityOS.IntegrationTests.Hosts.Api;

/// <summary>
/// <c>/api/v1/service-requests</c> for guests (J3, spec §33 service status): a guest reads their own stay's unresolved
/// requests, newest first, with where each is but not who is doing it; a token for a stay that is not active reads
/// nothing; and the endpoint is the guest's alone.
/// </summary>
public sealed class GuestRequestEndpointsTests : IAsyncLifetime
{
    private const string Route = "/api/v1/service-requests";

    private static readonly GuestStay alex = new(Seed.Alex, Seed.AlexStay, Seed.PropertyId);

    private readonly StaffApi api = new(seedAuroraGrand: true, dispatchEvents: true, configure: ReferenceHotelsScriptTests.Scripted);

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() =>
        await api.HandledAsync(Seed.TenantId, "StaffMemberCreated", DutyRerouting.JoinedName, Cancellation);

    public ValueTask DisposeAsync() => api.DisposeAsync();

    [Fact]
    public async Task AGuest_ReadsTheirStaysOpenRequests_NewestFirst()
    {
        using HttpClient guest = api.Guest(Seed.TenantId, alex);
        Assert.Empty(await StaffApi.OkAsync<IReadOnlyList<GuestRequestResponse>>(await guest.GetAsync(Route, Cancellation), Cancellation));

        await AskAsync(api, guest, "Could I get two towels please?", Cancellation);
        // A minute later, so the newest is clear whatever the dispatcher's timing.
        api.Clock.Advance(TimeSpan.FromMinutes(1));
        await AskAsync(api, guest, "The air con is not working", Cancellation);

        IReadOnlyList<GuestRequestResponse> requests = await StaffApi.OkAsync<IReadOnlyList<GuestRequestResponse>>(await guest.GetAsync(Route, Cancellation), Cancellation);
        (string, string?, string?, string, GuestRequestStatus)[] expected =
        [
            ("Repairs", null, "The air conditioning is not working", "Room 412", GuestRequestStatus.Open),
            ("Housekeeping amenities", "Bath towel", null, "Room 412", GuestRequestStatus.Open),
        ];
        Assert.Equal(expected, requests.Select(request => (request.ServiceName, request.ItemName, request.Description, request.Location, request.Status)));
    }

    [Fact]
    public async Task AStayThatIsNotActive_ReadsNothing()
    {
        using HttpClient stranger = api.Guest(Seed.TenantId, new GuestStay(new GuestId(Guid.NewGuid()), new StayContextId(Guid.NewGuid()), Seed.PropertyId));

        Assert.Equal((HttpStatusCode.Forbidden, "guest.stay_not_active"), await StaffApi.ProblemAsync(await stranger.GetAsync(Route, Cancellation), Cancellation));
    }

    [Fact]
    public async Task OnlyGuests_ReadTheirRequests()
    {
        using HttpClient manager = api.Staff(Seed.TenantId, "Manager");
        using HttpClient anonymous = api.Client();

        Assert.Equal(HttpStatusCode.Forbidden, (await manager.GetAsync(Route, Cancellation)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(Route, Cancellation)).StatusCode);
    }
}
