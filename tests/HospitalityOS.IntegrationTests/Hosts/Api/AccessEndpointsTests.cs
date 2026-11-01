using System.Net;
using System.Net.Http.Json;
using HospitalityOS.Contracts.Api.Access;
using HospitalityOS.Contracts.Api.Properties;
using HospitalityOS.Contracts.Api.Staff;
using HospitalityOS.Contracts.Serialization;
using HospitalityOS.Ports.Auditing;
using HospitalityOS.SharedKernel.Execution;
using HospitalityOS.SharedKernel.Ids;
using HospitalityOS.SharedKernel.Tenancy;
using Xunit;

namespace HospitalityOS.IntegrationTests.Hosts.Api;

/// <summary>
/// Signing in through the identity provider (K4b): a sign-in acts as nobody until it redeems an administrator's invite,
/// then as that access, with its tenant and role, on every request until the access is revoked; workers act as their
/// roster entry; codes work once and expire; and operators invite a customer's first administrator, once.
/// </summary>
public sealed class AccessEndpointsTests : IAsyncDisposable
{
    private const string Route = "/api/v1/access";
    private const string Me = "/api/v1/me";

    private readonly StaffApi api = new();
    private readonly TenantId tenant = new(Guid.CreateVersion7());

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    public ValueTask DisposeAsync() => api.DisposeAsync();

    [Fact]
    public async Task ASignIn_ActsAsNobody_UntilItRedeemsAnInvite()
    {
        using HttpClient dana = api.SignIn("dana");

        SignInResponse before = await MeAsync(dana);
        using HttpResponseMessage organizations = await dana.GetAsync("/api/v1/organizations", Cancellation);
        using HttpResponseMessage access = await dana.GetAsync(Route, Cancellation);

        Assert.Equal(new SignInResponse(false, false, null, null, null, null, null), before);
        Assert.Equal(HttpStatusCode.Forbidden, organizations.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, access.StatusCode);
    }

    [Fact]
    public async Task ARedeemedInvite_MakesTheSignInActAsTheAccess_InItsTenant()
    {
        using HttpClient admin = api.Staff(tenant);
        StaffInviteResponse invite = await GrantAsync(admin, new GrantStaffAccessRequest(" Dana Reyes ", StaffRole.Manager));
        using HttpClient dana = api.SignIn("dana");

        StaffAccessResponse redeemed = await RedeemAsync(dana, invite.Code.ToLowerInvariant().Replace('-', ' '));
        SignInResponse me = await MeAsync(dana);
        using HttpResponseMessage organizations = await dana.GetAsync("/api/v1/organizations", Cancellation);
        using HttpResponseMessage grant = await dana.PostAsJsonAsync(Route, new GrantStaffAccessRequest("Jo", StaffRole.Manager), ContractJson.Options, Cancellation);

        Assert.Equal((StaffAccessStatus.Invited, "Dana Reyes"), (invite.Access.Status, invite.Access.DisplayName));
        Assert.Equal(StaffAccessStatus.Active, redeemed.Status);
        Assert.Equal(new SignInResponse(true, false, tenant.Value, invite.Access.Id, "Dana Reyes", StaffRole.Manager, null), me);
        Assert.Equal(HttpStatusCode.OK, organizations.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, grant.StatusCode);
        AuditRecord audit = Assert.Single(await api.AuditAsync(tenant, Cancellation), record => record.Action == "StaffAccess.Redeem");
        Assert.Equal((AuditOutcome.Succeeded, invite.Access.Id.ToString()), (audit.Outcome, audit.Actor.Id));
    }

    [Fact]
    public async Task TheSameUser_ThroughAnotherApp_ActsAsTheSameAccess()
    {
        using HttpClient admin = api.Staff(tenant);
        StaffInviteResponse invite = await GrantAsync(admin, new GrantStaffAccessRequest("Dana", StaffRole.Manager));
        using HttpClient staffWeb = api.SignIn("dana", app: "staff-web");
        using HttpClient adminWeb = api.SignIn("dana", app: "admin-web");
        using HttpClient someoneElse = api.SignIn("jo", app: "staff-web");

        await RedeemAsync(staffWeb, invite.Code);

        Assert.Equal(invite.Access.Id, (await MeAsync(adminWeb)).AccessId);
        Assert.False((await MeAsync(someoneElse)).IsLinked);
    }

    [Fact]
    public async Task AWorker_ActsAsTheirRosterEntry()
    {
        PropertyResponse property = await api.CreatePropertyAsync(tenant, Cancellation);
        using HttpClient admin = api.Staff(tenant);
        using HttpResponseMessage created = await admin.PostAsJsonAsync("/api/v1/staff", new CreateStaffMemberRequest(PropertyId: property.Id, DisplayName: "Sam Patel"), ContractJson.Options, Cancellation);
        StaffMemberResponse sam = (await created.Content.ReadFromJsonAsync<StaffMemberResponse>(ContractJson.Options, Cancellation))!;
        using HttpResponseMessage nobody = await admin.PostAsJsonAsync(Route, new GrantStaffAccessRequest("Sam", StaffRole.Worker), ContractJson.Options, Cancellation);
        using HttpResponseMessage unknown = await admin.PostAsJsonAsync(
            Route, new GrantStaffAccessRequest("Sam", StaffRole.Worker, new StaffMemberId(Guid.CreateVersion7())), ContractJson.Options, Cancellation);
        StaffInviteResponse invite = await GrantAsync(admin, new GrantStaffAccessRequest("Sam Patel", StaffRole.Worker, sam.Id));
        using HttpResponseMessage twice = await admin.PostAsJsonAsync(Route, new GrantStaffAccessRequest("Sam", StaffRole.Worker, sam.Id), ContractJson.Options, Cancellation);
        using HttpClient samSignIn = api.SignIn("sam");

        await RedeemAsync(samSignIn, invite.Code);
        using HttpResponseMessage duty = await samSignIn.PutAsJsonAsync($"/api/v1/staff/{sam.Id}/duty", new SetDutyRequest(true), ContractJson.Options, Cancellation);

        Assert.Equal(["access.staff_member_required"], await StaffApi.FieldCodesAsync(nobody, Cancellation));
        Assert.Equal(["access.staff_member_not_found"], await StaffApi.FieldCodesAsync(unknown, Cancellation));
        Assert.Equal((HttpStatusCode.Conflict, "access.staff_member_taken"), await StaffApi.ProblemAsync(twice, Cancellation));
        Assert.Equal(sam.Id, (await MeAsync(samSignIn)).StaffMemberId);
        Assert.Equal(HttpStatusCode.OK, duty.StatusCode);
    }

    [Fact]
    public async Task ACode_WorksOnce_ForOneSignIn_AndNotOnceExpired()
    {
        using HttpClient admin = api.Staff(tenant);
        StaffInviteResponse dana = await GrantAsync(admin, new GrantStaffAccessRequest("Dana", StaffRole.Manager));
        StaffInviteResponse jo = await GrantAsync(admin, new GrantStaffAccessRequest("Jo", StaffRole.Manager));
        StaffInviteResponse late = await GrantAsync(admin, new GrantStaffAccessRequest("Late", StaffRole.Manager));
        using HttpClient danaSignIn = api.SignIn("dana");
        using HttpClient other = api.SignIn("other");
        await RedeemAsync(danaSignIn, dana.Code);

        using HttpResponseMessage reused = await other.PostAsJsonAsync($"{Me}/invite", new RedeemInviteRequest(dana.Code), ContractJson.Options, Cancellation);
        using HttpResponseMessage second = await danaSignIn.PostAsJsonAsync($"{Me}/invite", new RedeemInviteRequest(jo.Code), ContractJson.Options, Cancellation);
        using HttpResponseMessage wrong = await other.PostAsJsonAsync($"{Me}/invite", new RedeemInviteRequest("AAAAA-AAAAA-AAAAA-AAAAA"), ContractJson.Options, Cancellation);
        using HttpResponseMessage garbage = await other.PostAsJsonAsync($"{Me}/invite", new RedeemInviteRequest("not a code"), ContractJson.Options, Cancellation);
        using HttpResponseMessage staffToken = await admin.PostAsJsonAsync($"{Me}/invite", new RedeemInviteRequest(jo.Code), ContractJson.Options, Cancellation);
        api.Clock.Advance(TimeSpan.FromDays(8));
        using HttpResponseMessage expired = await other.PostAsJsonAsync($"{Me}/invite", new RedeemInviteRequest(late.Code), ContractJson.Options, Cancellation);

        Assert.Equal((HttpStatusCode.NotFound, "access.invite_not_found"), await StaffApi.ProblemAsync(reused, Cancellation));
        Assert.Equal((HttpStatusCode.Conflict, "access.already_linked"), await StaffApi.ProblemAsync(second, Cancellation));
        Assert.Equal((HttpStatusCode.NotFound, "access.invite_not_found"), await StaffApi.ProblemAsync(wrong, Cancellation));
        Assert.Equal((HttpStatusCode.NotFound, "access.invite_not_found"), await StaffApi.ProblemAsync(garbage, Cancellation));
        Assert.Equal(HttpStatusCode.Forbidden, staffToken.StatusCode);
        Assert.Equal((HttpStatusCode.UnprocessableEntity, "access.invite_expired"), await StaffApi.ProblemAsync(expired, Cancellation));
        Assert.False((await MeAsync(other)).IsLinked);
    }

    [Fact]
    public async Task RevokedAccess_IsRefusedOnTheNextRequest_AndANewInviteRelinksSomeoneElse()
    {
        using HttpClient admin = api.Staff(tenant);
        StaffInviteResponse invite = await GrantAsync(admin, new GrantStaffAccessRequest("Dana", StaffRole.Manager));
        using HttpClient dana = api.SignIn("dana");
        await RedeemAsync(dana, invite.Code);

        StaffInviteResponse relinked = await PostAsync<StaffInviteResponse>(admin, $"{Route}/{invite.Access.Id}/invite");
        SignInResponse afterReinvite = await MeAsync(dana);
        using HttpClient newPhone = api.SignIn("dana-new-phone");
        await RedeemAsync(newPhone, relinked.Code);
        StaffAccessResponse revoked = await PostAsync<StaffAccessResponse>(admin, $"{Route}/{invite.Access.Id}/revoke");
        using HttpResponseMessage refused = await newPhone.GetAsync("/api/v1/organizations", Cancellation);
        using HttpResponseMessage again = await admin.PostAsync($"{Route}/{invite.Access.Id}/invite", null, Cancellation);
        List<StaffAccessResponse> listed = (await admin.GetFromJsonAsync<List<StaffAccessResponse>>(Route, ContractJson.Options, Cancellation))!;

        Assert.False(afterReinvite.IsLinked);
        Assert.Equal(StaffAccessStatus.Revoked, revoked.Status);
        Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);
        Assert.False((await MeAsync(newPhone)).IsLinked);
        Assert.Equal((HttpStatusCode.UnprocessableEntity, "access.revoked"), await StaffApi.ProblemAsync(again, Cancellation));
        Assert.Equal([StaffAccessStatus.Revoked], listed.Select(access => access.Status));
    }

    [Fact]
    public async Task AnAdministrator_CannotRevokeThemselves_AndOtherTenantsAccessDoesNotExist()
    {
        using HttpClient admin = api.Staff(tenant);
        StaffInviteResponse invite = await GrantAsync(admin, new GrantStaffAccessRequest("Priya", StaffRole.Admin));
        using HttpClient priya = api.SignIn("priya");
        await RedeemAsync(priya, invite.Code);
        using HttpClient otherAdmin = api.Staff(new TenantId(Guid.CreateVersion7()));

        using HttpResponseMessage self = await priya.PostAsync($"{Route}/{invite.Access.Id}/revoke", null, Cancellation);
        using HttpResponseMessage elsewhere = await otherAdmin.PostAsync($"{Route}/{invite.Access.Id}/revoke", null, Cancellation);

        Assert.Equal((HttpStatusCode.Forbidden, "access.revoke_self"), await StaffApi.ProblemAsync(self, Cancellation));
        Assert.Equal((HttpStatusCode.NotFound, "access.not_found"), await StaffApi.ProblemAsync(elsewhere, Cancellation));
    }

    [Fact]
    public async Task Operators_InviteACustomersFirstAdministrator_Once()
    {
        string first = $"/api/v1/operator/tenants/{tenant.Value}/admins";
        using HttpClient operatorSignIn = api.SignIn("ops", isOperator: true);
        using HttpClient plainSignIn = api.SignIn("someone");
        using HttpClient staffAdmin = api.Staff(tenant);

        SignInResponse me = await MeAsync(operatorSignIn);
        using HttpResponseMessage created = await operatorSignIn.PostAsJsonAsync(first, new GrantFirstAdminRequest("Priya Shah"), ContractJson.Options, Cancellation);
        StaffInviteResponse invite = (await created.Content.ReadFromJsonAsync<StaffInviteResponse>(ContractJson.Options, Cancellation))!;
        using HttpResponseMessage twice = await operatorSignIn.PostAsJsonAsync(first, new GrantFirstAdminRequest("Someone Else"), ContractJson.Options, Cancellation);
        using HttpResponseMessage notOperator = await plainSignIn.PostAsJsonAsync(first, new GrantFirstAdminRequest("Me"), ContractJson.Options, Cancellation);
        using HttpResponseMessage notSignIn = await staffAdmin.PostAsJsonAsync(first, new GrantFirstAdminRequest("Me"), ContractJson.Options, Cancellation);
        using HttpClient priya = api.SignIn("priya");
        await RedeemAsync(priya, invite.Code);

        Assert.Equal(new SignInResponse(false, true, null, null, null, null, null), me);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal((StaffRole.Admin, "Priya Shah"), (invite.Access.Role, invite.Access.DisplayName));
        Assert.Equal((HttpStatusCode.Conflict, "access.tenant_has_admin"), await StaffApi.ProblemAsync(twice, Cancellation));
        Assert.Equal(HttpStatusCode.Forbidden, notOperator.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, notSignIn.StatusCode);
        Assert.Equal((tenant.Value, StaffRole.Admin), ((await MeAsync(priya)).TenantId, (await MeAsync(priya)).Role));
        Assert.Contains(await api.AuditAsync(tenant, Cancellation), record => record.Action == "StaffAccess.GrantFirstAdmin" && record.Actor.Id == "operator:ops");
    }

    [Fact]
    public async Task StayTokens_AndTokensFromAnUntrustedIssuer_AreRefused()
    {
        using HttpClient guest = api.Guest(tenant, new GuestStay(new GuestId(Guid.CreateVersion7()), new StayContextId(Guid.CreateVersion7()), new PropertyId(Guid.CreateVersion7())));
        using HttpClient forged = api.Client(StaffApi.Token(new Dictionary<string, object> { ["oid"] = "dana" }, issuer: "https://elsewhere.example/v2.0"));

        using HttpResponseMessage guestMe = await guest.GetAsync(Me, Cancellation);
        using HttpResponseMessage forgedMe = await forged.GetAsync(Me, Cancellation);

        Assert.Equal(HttpStatusCode.Forbidden, guestMe.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, forgedMe.StatusCode);
    }

    private static async Task<StaffInviteResponse> GrantAsync(HttpClient admin, GrantStaffAccessRequest request)
    {
        using HttpResponseMessage response = await admin.PostAsJsonAsync(Route, request, ContractJson.Options, Cancellation);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<StaffInviteResponse>(ContractJson.Options, Cancellation))!;
    }

    private static async Task<StaffAccessResponse> RedeemAsync(HttpClient signIn, string code)
    {
        using HttpResponseMessage response = await signIn.PostAsJsonAsync($"{Me}/invite", new RedeemInviteRequest(code), ContractJson.Options, Cancellation);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<StaffAccessResponse>(ContractJson.Options, Cancellation))!;
    }

    private static async Task<SignInResponse> MeAsync(HttpClient client) =>
        (await client.GetFromJsonAsync<SignInResponse>(Me, ContractJson.Options, Cancellation))!;

    private static async Task<T> PostAsync<T>(HttpClient client, string path)
    {
        using HttpResponseMessage response = await client.PostAsync(path, null, Cancellation);
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync(Cancellation));
        return (await response.Content.ReadFromJsonAsync<T>(ContractJson.Options, Cancellation))!;
    }
}
