using System.Net;
using HospitalityOS.Contracts.Api.Stays;
using HospitalityOS.SharedKernel.Ids;
using Xunit;
using Mysore = HospitalityOS.Seeding.MysoreGrand;
using Seed = HospitalityOS.Seeding.AuroraGrand;

namespace HospitalityOS.IntegrationTests.Hosts.Api;

/// <summary>
/// <c>/api/v1/stay</c>: a guest reads their own stay (which hotel, which room, and when), so the guest app shows the right
/// hotel; a token for a stay that is not active reads nothing; and the endpoint is the guest's alone.
/// </summary>
public sealed class GuestStayEndpointsTests : IAsyncDisposable
{
    private const string Route = "/api/v1/stay";

    private readonly StaffApi api = new(seedAuroraGrand: true);

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    public ValueTask DisposeAsync() => api.DisposeAsync();

    [Fact]
    public async Task EachGuest_ReadsTheirOwnHotelAndRoom()
    {
        using HttpClient alex = api.Guest(Seed.TenantId, new GuestStay(Seed.Alex, Seed.AlexStay, Seed.PropertyId));
        using HttpClient ananya = api.Guest(Seed.TenantId, new GuestStay(Mysore.Ananya, Mysore.AnanyaStay, Mysore.PropertyId));

        GuestStayResponse atAurora = await StaffApi.OkAsync<GuestStayResponse>(await alex.GetAsync(Route, Cancellation), Cancellation);
        GuestStayResponse atMysore = await StaffApi.OkAsync<GuestStayResponse>(await ananya.GetAsync(Route, Cancellation), Cancellation);

        Assert.Equal((Seed.AlexStay, Seed.PropertyId, "Aurora Grand", "412"), (atAurora.Id, atAurora.PropertyId, atAurora.PropertyName, atAurora.UnitNumber));
        Assert.Equal((Mysore.AnanyaStay, Mysore.PropertyId, "Mysore Grand", "210"), (atMysore.Id, atMysore.PropertyId, atMysore.PropertyName, atMysore.UnitNumber));
        Assert.Equal(2, atMysore.Departure.DayNumber - atMysore.Arrival.DayNumber);
    }

    [Fact]
    public async Task AStayThatIsNotActive_ReadsNothing()
    {
        using HttpClient stranger = api.Guest(Seed.TenantId, new GuestStay(new GuestId(Guid.NewGuid()), new StayContextId(Guid.NewGuid()), Seed.PropertyId));

        Assert.Equal((HttpStatusCode.Forbidden, "guest.stay_not_active"), await StaffApi.ProblemAsync(await stranger.GetAsync(Route, Cancellation), Cancellation));
    }

    [Fact]
    public async Task OnlyGuests_ReadTheirStay()
    {
        using HttpClient manager = api.Staff(Seed.TenantId, "Manager");
        using HttpClient anonymous = api.Client();

        Assert.Equal(HttpStatusCode.Forbidden, (await manager.GetAsync(Route, Cancellation)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(Route, Cancellation)).StatusCode);
    }
}
