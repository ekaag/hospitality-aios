using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using HospitalityOS.Contracts.Api.GuestSessions;
using HospitalityOS.Contracts.Api.Properties;
using HospitalityOS.Contracts.Api.Stays;
using HospitalityOS.Contracts.Serialization;
using HospitalityOS.Domain.Guests.StayContexts;
using HospitalityOS.Ports.Persistence;
using HospitalityOS.Ports.Tenancy;
using HospitalityOS.SharedKernel.Tenancy;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Xunit;
using DomainStayStatus = HospitalityOS.Domain.Guests.StayContexts.StayStatus;

namespace HospitalityOS.IntegrationTests.Hosts.Api;

/// <summary>
/// Check-in links and stay tokens (K4c): the front desk sees the property's active stays and gives one a link; the link
/// opens sessions on any device while the stay is reserved or in house; replacing or revoking the link, or checking out,
/// ends its code and every session it opened at once; and only the API's own key signs stay tokens.
/// </summary>
public sealed class StayEndpointsTests : IAsyncDisposable
{
    private const string Route = "/api/v1/stays";
    private const string Sessions = "/api/v1/guest-sessions";
    private const string GuestOnly = "/api/v1/service-requests";

    private readonly StaffApi api = new(configure: builder => builder.UseSetting("GuestLinks:BaseUrl", "https://guest.example/"));
    private readonly TenantId tenant = new(Guid.CreateVersion7());

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    public ValueTask DisposeAsync() => api.DisposeAsync();

    [Fact]
    public async Task TheFrontDesk_GivesAnActiveStayALink_ThatOpensSessionsOnAnyDevice()
    {
        PropertyResponse property = await api.CreatePropertyAsync(tenant, Cancellation);
        GuestStay alex = await GuestStay.SaveAsync(api.Factory.Services, tenant, property.Id, StaffApi.Now, Cancellation);
        await GuestStay.SaveAsync(api.Factory.Services, tenant, property.Id, StaffApi.Now, Cancellation, DomainStayStatus.CheckedOut);
        using HttpClient manager = api.Staff(tenant, "Manager");

        List<StayResponse> before = await StaysAsync(manager, property);
        GuestLinkResponse link = await CreateLinkAsync(manager, alex);
        StayTokenResponse phone = await OpenAsync(link.Code);
        StayTokenResponse tablet = await OpenAsync(link.Code.ToLowerInvariant().Replace("-", " ", StringComparison.Ordinal));
        using HttpClient phoneClient = api.Client(phone.AccessToken);
        using HttpClient tabletClient = api.Client(tablet.AccessToken);
        using HttpResponseMessage fromPhone = await phoneClient.GetAsync(GuestOnly, Cancellation);
        using HttpResponseMessage fromTablet = await tabletClient.GetAsync(GuestOnly, Cancellation);
        using HttpResponseMessage staffOnly = await phoneClient.GetAsync($"{Route}?propertyId={property.Id}", Cancellation);

        Assert.Equal([(alex.StayContextId, "Alex", false)], before.Select(stay => (stay.Id, stay.GuestName, stay.HasGuestLink)));
        Assert.Equal($"https://guest.example/#link={link.Code}", link.Url);
        Assert.Equal((HttpStatusCode.OK, HttpStatusCode.OK), (fromPhone.StatusCode, fromTablet.StatusCode));
        Assert.Equal(HttpStatusCode.Forbidden, staffOnly.StatusCode);
        Assert.True((await StaysAsync(manager, property)).Single().HasGuestLink);
        Assert.InRange(phone.ExpiresAt - DateTimeOffset.UtcNow, TimeSpan.FromHours(7.9), TimeSpan.FromHours(8.1));

        JsonWebToken token = new(phone.AccessToken);
        Assert.Equal(("Guest", alex.StayContextId.Value.ToString(), alex.GuestId.Value.ToString()), (token.GetClaim("role").Value, token.GetClaim("stay_id").Value, token.Subject));
        Assert.Contains(await api.AuditAsync(tenant, Cancellation), record => record.Action == "GuestLink.Create" && record.TargetId == alex.StayContextId.Value.ToString());
    }

    [Fact]
    public async Task ReplacingALink_EndsTheOldCodeAndItsSessions()
    {
        PropertyResponse property = await api.CreatePropertyAsync(tenant, Cancellation);
        GuestStay alex = await GuestStay.SaveAsync(api.Factory.Services, tenant, property.Id, StaffApi.Now, Cancellation);
        using HttpClient manager = api.Staff(tenant, "Manager");
        GuestLinkResponse first = await CreateLinkAsync(manager, alex);
        using HttpClient oldSession = api.Client((await OpenAsync(first.Code)).AccessToken);

        GuestLinkResponse second = await CreateLinkAsync(manager, alex);
        using HttpResponseMessage oldCode = await api.Client().PostAsJsonAsync(Sessions, new OpenGuestSessionRequest(first.Code), ContractJson.Options, Cancellation);
        using HttpResponseMessage oldToken = await oldSession.GetAsync(GuestOnly, Cancellation);
        using HttpClient newSession = api.Client((await OpenAsync(second.Code)).AccessToken);
        using HttpResponseMessage newToken = await newSession.GetAsync(GuestOnly, Cancellation);

        Assert.NotEqual(first.Code, second.Code);
        Assert.Equal((HttpStatusCode.NotFound, "guest_link.not_found"), await StaffApi.ProblemAsync(oldCode, Cancellation));
        Assert.Equal(HttpStatusCode.Unauthorized, oldToken.StatusCode);
        Assert.Equal(HttpStatusCode.OK, newToken.StatusCode);
    }

    [Fact]
    public async Task RevokingTheLink_OrCheckingOut_EndsItsSessionsAtOnce()
    {
        PropertyResponse property = await api.CreatePropertyAsync(tenant, Cancellation);
        GuestStay alex = await GuestStay.SaveAsync(api.Factory.Services, tenant, property.Id, StaffApi.Now, Cancellation);
        GuestStay sam = await GuestStay.SaveAsync(api.Factory.Services, tenant, property.Id, StaffApi.Now, Cancellation);
        using HttpClient manager = api.Staff(tenant, "Manager");
        GuestLinkResponse alexLink = await CreateLinkAsync(manager, alex);
        GuestLinkResponse samLink = await CreateLinkAsync(manager, sam);
        using HttpClient alexSession = api.Client((await OpenAsync(alexLink.Code)).AccessToken);
        using HttpClient samSession = api.Client((await OpenAsync(samLink.Code)).AccessToken);

        using HttpResponseMessage revoked = await manager.DeleteAsync($"{Route}/{alex.StayContextId}/guest-link", Cancellation);
        await CheckOutAsync(sam);
        using HttpResponseMessage afterRevoke = await alexSession.GetAsync(GuestOnly, Cancellation);
        using HttpResponseMessage afterCheckout = await samSession.GetAsync(GuestOnly, Cancellation);
        using HttpResponseMessage reopenRevoked = await api.Client().PostAsJsonAsync(Sessions, new OpenGuestSessionRequest(alexLink.Code), ContractJson.Options, Cancellation);
        using HttpResponseMessage reopenEnded = await api.Client().PostAsJsonAsync(Sessions, new OpenGuestSessionRequest(samLink.Code), ContractJson.Options, Cancellation);
        using HttpResponseMessage linkEnded = await manager.PostAsync($"{Route}/{sam.StayContextId}/guest-link", null, Cancellation);

        Assert.False((await revoked.Content.ReadFromJsonAsync<StayResponse>(ContractJson.Options, Cancellation))!.HasGuestLink);
        Assert.Equal((HttpStatusCode.Unauthorized, HttpStatusCode.Unauthorized), (afterRevoke.StatusCode, afterCheckout.StatusCode));
        Assert.Equal((HttpStatusCode.NotFound, "guest_link.not_found"), await StaffApi.ProblemAsync(reopenRevoked, Cancellation));
        Assert.Equal((HttpStatusCode.UnprocessableEntity, "guest_link.stay_ended"), await StaffApi.ProblemAsync(reopenEnded, Cancellation));
        Assert.Equal((HttpStatusCode.UnprocessableEntity, "guest_link.stay_ended"), await StaffApi.ProblemAsync(linkEnded, Cancellation));
    }

    [Fact]
    public async Task OnlyTheFrontDesk_OfTheStaysTenant_ManagesLinks()
    {
        PropertyResponse property = await api.CreatePropertyAsync(tenant, Cancellation);
        GuestStay alex = await GuestStay.SaveAsync(api.Factory.Services, tenant, property.Id, StaffApi.Now, Cancellation);
        using HttpClient worker = api.Staff(tenant, "Worker");
        using HttpClient otherTenant = api.Staff(new TenantId(Guid.CreateVersion7()), "Manager");
        using HttpClient guest = api.Guest(tenant, alex);

        using HttpResponseMessage byWorker = await worker.PostAsync($"{Route}/{alex.StayContextId}/guest-link", null, Cancellation);
        using HttpResponseMessage byGuest = await guest.GetAsync($"{Route}?propertyId={property.Id}", Cancellation);
        using HttpResponseMessage elsewhere = await otherTenant.PostAsync($"{Route}/{alex.StayContextId}/guest-link", null, Cancellation);
        using HttpResponseMessage nonsense = await api.Client().PostAsJsonAsync(Sessions, new OpenGuestSessionRequest("not a code"), ContractJson.Options, Cancellation);

        Assert.Equal((HttpStatusCode.Forbidden, HttpStatusCode.Forbidden), (byWorker.StatusCode, byGuest.StatusCode));
        Assert.Equal((HttpStatusCode.NotFound, "stay.not_found"), await StaffApi.ProblemAsync(elsewhere, Cancellation));
        Assert.Equal((HttpStatusCode.NotFound, "guest_link.not_found"), await StaffApi.ProblemAsync(nonsense, Cancellation));
    }

    [Fact]
    public async Task AStayToken_SignedByAnyOtherKey_IsRefused()
    {
        PropertyResponse property = await api.CreatePropertyAsync(tenant, Cancellation);
        GuestStay alex = await GuestStay.SaveAsync(api.Factory.Services, tenant, property.Id, StaffApi.Now, Cancellation);
        using HttpClient manager = api.Staff(tenant, "Manager");
        JsonWebToken genuine = new((await OpenAsync((await CreateLinkAsync(manager, alex)).Code)).AccessToken);
        using RSA other = RSA.Create(2048);
        string forged = new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = genuine.Issuer,
            Audience = genuine.Audiences.Single(),
            Expires = DateTime.UtcNow.AddHours(1),
            Claims = genuine.Claims.Where(claim => claim.Type is "sub" or "tenant_id" or "role" or "property_id" or "stay_id" or "link_id").ToDictionary(claim => claim.Type, claim => (object)claim.Value),
            SigningCredentials = new SigningCredentials(new RsaSecurityKey(other) { KeyId = genuine.Kid }, SecurityAlgorithms.RsaSha256),
        });
        using HttpClient forger = api.Client(forged);

        using HttpResponseMessage response = await forger.GetAsync(GuestOnly, Cancellation);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private async Task<StayTokenResponse> OpenAsync(string code)
    {
        using HttpClient anonymous = api.Client();
        using HttpResponseMessage response = await anonymous.PostAsJsonAsync(Sessions, new OpenGuestSessionRequest(code), ContractJson.Options, Cancellation);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<StayTokenResponse>(ContractJson.Options, Cancellation))!;
    }

    private static async Task<GuestLinkResponse> CreateLinkAsync(HttpClient manager, GuestStay stay)
    {
        using HttpResponseMessage response = await manager.PostAsync($"{Route}/{stay.StayContextId}/guest-link", null, Cancellation);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<GuestLinkResponse>(ContractJson.Options, Cancellation))!;
    }

    private static async Task<List<StayResponse>> StaysAsync(HttpClient manager, PropertyResponse property) =>
        (await manager.GetFromJsonAsync<List<StayResponse>>($"{Route}?propertyId={property.Id}", ContractJson.Options, Cancellation))!;

    private async Task CheckOutAsync(GuestStay stay)
    {
        await using AsyncServiceScope scope = api.Factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().Set(tenant);
        StayContext loaded = (await scope.ServiceProvider.GetRequiredService<IStayContextRepository>().GetByIdAsync(stay.StayContextId, Cancellation))!;
        Assert.True(loaded.CheckOut(StaffApi.Now).IsSuccess);
        Assert.True((await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().CommitAsync(Cancellation)).IsSuccess);
    }
}
