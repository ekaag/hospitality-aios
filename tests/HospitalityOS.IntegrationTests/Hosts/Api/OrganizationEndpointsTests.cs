using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HospitalityOS.Contracts.Api.Organizations;
using HospitalityOS.Contracts.Serialization;
using HospitalityOS.Ports.Auditing;
using HospitalityOS.SharedKernel.Execution;
using HospitalityOS.SharedKernel.Tenancy;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace HospitalityOS.IntegrationTests.Hosts.Api;

/// <summary>
/// <c>/api/v1/organizations</c> through the real host and token validation: administrators create and rename, staff
/// read, other tenants see nothing, and every change or refused change is audited.
/// </summary>
public sealed class OrganizationEndpointsTests : IAsyncDisposable
{
    private const string Route = "/api/v1/organizations";

    private static readonly string[] workerAndAdmin = ["Worker", "Admin"];

    private readonly StaffApi api = new();
    private readonly TenantId tenant = new(Guid.CreateVersion7());

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    public ValueTask DisposeAsync() => api.DisposeAsync();

    [Fact]
    public async Task Create_AsAdmin_Returns201WithLocationAndETag_AndAuditsTheActor()
    {
        using HttpClient admin = api.Staff(tenant);

        using HttpResponseMessage response = await admin.PostAsJsonAsync(Route, new { name = "  Aurora Hotels  " }, Cancellation);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        OrganizationResponse created = (await response.Content.ReadFromJsonAsync<OrganizationResponse>(ContractJson.Options, Cancellation))!;
        Assert.Equal(("Aurora Hotels", 1L), (created.Name, created.Version));
        Assert.Equal($"{Route}/{created.Id}", response.Headers.Location?.OriginalString);
        Assert.Equal("\"1\"", response.Headers.ETag?.Tag);
        AuditRecord audit = Assert.Single(await api.AuditAsync(tenant, Cancellation));
        Assert.Equal(
            ("Organization.Create", AuditOutcome.Succeeded, Actor.Staff("maria", StaffRole.Admin), "Organization", created.Id.ToString(), StaffApi.Now),
            (audit.Action, audit.Outcome, audit.Actor, audit.TargetType, audit.TargetId, audit.OccurredAt));
    }

    [Fact]
    public async Task Create_WithBlankName_Returns400WithTheFieldError_PersistsNothing_AndAuditsTheFailure()
    {
        using HttpClient admin = api.Staff(tenant);

        using HttpResponseMessage response = await admin.PostAsJsonAsync(Route, new { name = " " }, Cancellation);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using JsonDocument problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Cancellation));
        Assert.Equal("Name is required.", problem.RootElement.GetProperty("errors").GetProperty("name")[0].GetString());
        Assert.Equal("organization.name_required", problem.RootElement.GetProperty("fieldErrors")[0].GetProperty("code").GetString());
        Assert.Empty(await admin.GetFromJsonAsync<List<OrganizationResponse>>(Route, ContractJson.Options, Cancellation) ?? []);
        AuditRecord audit = Assert.Single(await api.AuditAsync(tenant, Cancellation));
        Assert.Equal(("Organization.Create", AuditOutcome.Failed, "validation.failed"), (audit.Action, audit.Outcome, audit.Detail));
    }

    [Fact]
    public async Task ListAndGet_ShowOnlyTheCallersTenant_ToAnyStaffRole()
    {
        OrganizationResponse harbour = await CreateAsync(tenant, "Harbour Group");
        OrganizationResponse aurora = await CreateAsync(tenant, "aurora Hotels");
        OrganizationResponse other = await CreateAsync(new TenantId(Guid.CreateVersion7()), "Other Group");
        using HttpClient worker = api.Staff(tenant, "Worker", "sam");

        List<OrganizationResponse>? listed = await worker.GetFromJsonAsync<List<OrganizationResponse>>(Route, ContractJson.Options, Cancellation);
        using HttpResponseMessage mine = await worker.GetAsync($"{Route}/{aurora.Id}", Cancellation);
        using HttpResponseMessage theirs = await worker.GetAsync($"{Route}/{other.Id}", Cancellation);

        Assert.Equal([aurora, harbour], listed);
        Assert.Equal((HttpStatusCode.OK, "\"1\""), (mine.StatusCode, mine.Headers.ETag?.Tag));
        Assert.Equal(HttpStatusCode.NotFound, theirs.StatusCode);
    }

    [Fact]
    public async Task Rename_WithTheCurrentVersion_Succeeds_ButAStaleOneIs412AndChangesNothing()
    {
        OrganizationResponse created = await CreateAsync(tenant, "Aurora Hotels");
        using HttpClient admin = api.Staff(tenant);

        using HttpResponseMessage renamed = await PutAsync(admin, $"{Route}/{created.Id}", new { name = "Aurora Hotels Group" }, "\"1\"");
        using HttpResponseMessage stale = await PutAsync(admin, $"{Route}/{created.Id}", new { name = "Aurora Resorts" }, "\"1\"");
        using HttpResponseMessage malformed = await PutAsync(admin, $"{Route}/{created.Id}", new { name = "Aurora Resorts" }, "W/\"x\"");

        Assert.Equal((HttpStatusCode.OK, "\"2\""), (renamed.StatusCode, renamed.Headers.ETag?.Tag));
        Assert.Equal(HttpStatusCode.PreconditionFailed, stale.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, malformed.StatusCode);
        OrganizationResponse? current = await admin.GetFromJsonAsync<OrganizationResponse>($"{Route}/{created.Id}", ContractJson.Options, Cancellation);
        Assert.Equal(("Aurora Hotels Group", 2L), (current?.Name, current?.Version));
        Assert.Equal(
            [("Organization.Create", AuditOutcome.Succeeded), ("Organization.Rename", AuditOutcome.Succeeded), ("Organization.Rename", AuditOutcome.Failed)],
            (await api.AuditAsync(tenant, Cancellation)).Select(audit => (audit.Action, audit.Outcome)));
    }

    [Fact]
    public async Task Rename_AnotherTenantsOrganization_Is404()
    {
        OrganizationResponse other = await CreateAsync(new TenantId(Guid.CreateVersion7()), "Other Group");
        using HttpClient admin = api.Staff(tenant);

        using HttpResponseMessage response = await PutAsync(admin, $"{Route}/{other.Id}", new { name = "Taken over" }, ifMatch: null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData("Manager")]
    [InlineData("Worker")]
    public async Task Create_AsNonAdmin_Is403_AndAuditedAsDenied(string role)
    {
        using HttpClient staff = api.Staff(tenant, role, "sam");

        using HttpResponseMessage response = await staff.PostAsJsonAsync(Route, new { name = "Aurora Hotels" }, Cancellation);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        AuditRecord audit = Assert.Single(await api.AuditAsync(tenant, Cancellation));
        Assert.Equal(("Organization.Create", AuditOutcome.Denied, "sam"), (audit.Action, audit.Outcome, audit.Actor.Id));
    }

    [Fact]
    public async Task Requests_WithoutAUsableToken_Are401()
    {
        string noTenant = StaffApi.Token(new Dictionary<string, object> { ["sub"] = "maria", ["role"] = "Admin" });
        string unknownRole = StaffApi.Token(new Dictionary<string, object> { ["sub"] = "maria", ["tenant_id"] = tenant.Value.ToString(), ["role"] = "Owner" });
        string forged = StaffApi.Token(
            new Dictionary<string, object> { ["sub"] = "maria", ["tenant_id"] = tenant.Value.ToString(), ["role"] = "Admin" },
            new SymmetricSecurityKey(new byte[32]));

        foreach (string? token in new[] { null, noTenant, unknownRole, forged })
        {
            using HttpClient client = api.Client(token);
            using HttpResponseMessage response = await client.GetAsync(Route, Cancellation);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
    }

    [Fact]
    public async Task ATokenWithSeveralRoles_ActsWithTheHighest()
    {
        string token = StaffApi.Token(new Dictionary<string, object> { ["sub"] = "maria", ["tenant_id"] = tenant.Value.ToString(), ["role"] = workerAndAdmin });
        using HttpClient client = api.Client(token);

        using HttpResponseMessage response = await client.PostAsJsonAsync(Route, new { name = "Aurora Hotels" }, Cancellation);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(Actor.Staff("maria", StaffRole.Admin), Assert.Single(await api.AuditAsync(tenant, Cancellation)).Actor);
    }

    private async Task<OrganizationResponse> CreateAsync(TenantId owner, string name)
    {
        using HttpClient admin = api.Staff(owner);
        using HttpResponseMessage response = await admin.PostAsJsonAsync(Route, new { name }, Cancellation);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        return (await response.Content.ReadFromJsonAsync<OrganizationResponse>(ContractJson.Options, Cancellation))!;
    }

    private static async Task<HttpResponseMessage> PutAsync(HttpClient client, string uri, object body, string? ifMatch)
    {
        using HttpRequestMessage request = new(HttpMethod.Put, uri) { Content = JsonContent.Create(body) };

        if (ifMatch is not null)
        {
            request.Headers.TryAddWithoutValidation("If-Match", ifMatch);
        }

        return await client.SendAsync(request, Cancellation);
    }
}
