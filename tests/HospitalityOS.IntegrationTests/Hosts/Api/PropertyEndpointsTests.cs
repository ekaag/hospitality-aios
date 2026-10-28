using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HospitalityOS.Contracts.Api.Organizations;
using HospitalityOS.Contracts.Api.Properties;
using HospitalityOS.Contracts.Serialization;
using HospitalityOS.Ports.Auditing;
using HospitalityOS.Ports.Eventing;
using HospitalityOS.SharedKernel.Execution;
using HospitalityOS.SharedKernel.Tenancy;
using Xunit;

namespace HospitalityOS.IntegrationTests.Hosts.Api;

/// <summary>
/// Journey 1 through the real host: an administrator creates and configures a property without touching code; invalid
/// input is reported field by field and saves nothing; creation emits <c>PropertyCreated</c> and is audited with actor,
/// action, time, and property; other tenants can neither read nor change it.
/// </summary>
public sealed class PropertyEndpointsTests : IAsyncDisposable
{
    private const string Route = "/api/v1/properties";

    private static readonly object strand = new { line1 = "1 Strand", city = "London", postalCode = "WC2N 5HR", countryCode = "gb" };

    private readonly StaffApi api = new();
    private readonly TenantId tenant = new(Guid.CreateVersion7());

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    public ValueTask DisposeAsync() => api.DisposeAsync();

    [Fact]
    public async Task Create_WithValidFields_Returns201_EmitsPropertyCreated_AndAuditsActorActionTimeAndProperty()
    {
        OrganizationResponse organization = await CreateOrganizationAsync(tenant);
        using HttpClient admin = api.Staff(tenant);

        using HttpResponseMessage response = await admin.PostAsJsonAsync(
            Route, new { organizationId = organization.Id.Value, name = "Aurora Grand", timeZone = "Europe/London", address = strand, contactPhone = "+44 20 7946 0000" }, Cancellation);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        PropertyResponse created = (await response.Content.ReadFromJsonAsync<PropertyResponse>(ContractJson.Options, Cancellation))!;
        Assert.Equal(
            (organization.Id, "Aurora Grand", "Europe/London", "GB", 1L),
            (created.OrganizationId, created.Name, created.TimeZone, created.Address.CountryCode, created.Version));
        EventMessage propertyCreated = Assert.Single(await api.EventsAsync(tenant, Cancellation), message => message.EventType == "PropertyCreated");
        Assert.Equal(created.Id.Value, propertyCreated.AggregateId);
        Assert.Contains("\"actorId\":\"maria\"", propertyCreated.Envelope, StringComparison.Ordinal);
        AuditRecord audit = (await api.AuditAsync(tenant, Cancellation)).Single(record => record.Action == "Property.Create");
        Assert.Equal(
            (AuditOutcome.Succeeded, Actor.Staff("maria", StaffRole.Admin), StaffApi.Now, created.Id, "Property", created.Id.ToString()),
            (audit.Outcome, audit.Actor, audit.OccurredAt, audit.PropertyId, audit.TargetType, audit.TargetId));
    }

    [Fact]
    public async Task Create_WithMissingAndInvalidFields_ReportsEveryField_AndSavesNothing()
    {
        OrganizationResponse organization = await CreateOrganizationAsync(tenant);
        using HttpClient admin = api.Staff(tenant);

        using HttpResponseMessage response = await admin.PostAsJsonAsync(
            Route, new { organizationId = organization.Id.Value, timeZone = "Mars/Olympus", contactEmail = "front desk" }, Cancellation);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using JsonDocument problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Cancellation));
        Assert.Equal(
            ["property.name_required", "property.time_zone_invalid", "property.address_required", "property.contact_email_invalid"],
            problem.RootElement.GetProperty("fieldErrors").EnumerateArray().Select(field => field.GetProperty("code").GetString()));
        Assert.Empty(await admin.GetFromJsonAsync<List<PropertyResponse>>(Route, ContractJson.Options, Cancellation) ?? []);
        Assert.DoesNotContain(await api.EventsAsync(tenant, Cancellation), message => message.EventType == "PropertyCreated");
        Assert.Contains(await api.AuditAsync(tenant, Cancellation), audit => (audit.Action, audit.Outcome) == ("Property.Create", AuditOutcome.Failed));
    }

    [Fact]
    public async Task Create_ForAnotherTenantsOrMissingOrganization_IsAnOrganizationIdError()
    {
        OrganizationResponse theirs = await CreateOrganizationAsync(new TenantId(Guid.CreateVersion7()));
        using HttpClient admin = api.Staff(tenant);

        using HttpResponseMessage other = await admin.PostAsJsonAsync(Route, new { organizationId = theirs.Id.Value, name = "Aurora Grand", timeZone = "Europe/London", address = strand }, Cancellation);
        using HttpResponseMessage missing = await admin.PostAsJsonAsync(Route, new { name = "Aurora Grand", timeZone = "Europe/London", address = strand }, Cancellation);

        Assert.Equal("property.organization_not_found", await FirstFieldCodeAsync(other));
        Assert.Equal("property.organization_required", await FirstFieldCodeAsync(missing));
    }

    [Fact]
    public async Task AnotherTenant_CanNeitherReadNorChangeTheProperty()
    {
        PropertyResponse property = await CreatePropertyAsync(tenant, "Aurora Grand");
        TenantId intruder = new(Guid.CreateVersion7());
        using HttpClient admin = api.Staff(intruder, "Admin", "mallory");

        using HttpResponseMessage read = await admin.GetAsync($"{Route}/{property.Id}", Cancellation);
        using HttpResponseMessage change = await admin.PutAsJsonAsync($"{Route}/{property.Id}", new { name = "Hijacked", timeZone = "Europe/London", address = strand }, Cancellation);
        List<PropertyResponse>? listed = await admin.GetFromJsonAsync<List<PropertyResponse>>(Route, ContractJson.Options, Cancellation);

        Assert.Equal((HttpStatusCode.NotFound, HttpStatusCode.NotFound), (read.StatusCode, change.StatusCode));
        Assert.Empty(listed ?? []);
        using HttpClient owner = api.Staff(tenant);
        PropertyResponse? unchanged = await owner.GetFromJsonAsync<PropertyResponse>($"{Route}/{property.Id}", ContractJson.Options, Cancellation);
        Assert.Equal(("Aurora Grand", 1L), (unchanged?.Name, unchanged?.Version));
    }

    [Fact]
    public async Task Update_WithTheCurrentVersion_EmitsPropertyUpdated_AndAStaleVersionIs412()
    {
        PropertyResponse property = await CreatePropertyAsync(tenant, "Aurora Grand");
        using HttpClient admin = api.Staff(tenant);

        using HttpResponseMessage updated = await SendAsync(admin, HttpMethod.Put, $"{Route}/{property.Id}", new { name = "The Aurora Grand", timeZone = "Europe/London", address = strand }, "\"1\"");
        using HttpResponseMessage stale = await SendAsync(admin, HttpMethod.Put, $"{Route}/{property.Id}", new { name = "Aurora", timeZone = "Europe/London", address = strand }, "\"1\"");

        Assert.Equal((HttpStatusCode.OK, "\"2\""), (updated.StatusCode, updated.Headers.ETag?.Tag));
        Assert.Equal(HttpStatusCode.PreconditionFailed, stale.StatusCode);
        Assert.Single(await api.EventsAsync(tenant, Cancellation), message => message.EventType == "PropertyUpdated");
    }

    [Fact]
    public async Task List_ByOrganization_ReturnsThatOrganizationsPropertiesByName()
    {
        OrganizationResponse aurora = await CreateOrganizationAsync(tenant, "Aurora Hotels");
        OrganizationResponse harbour = await CreateOrganizationAsync(tenant, "Harbour Group");
        await CreatePropertyAsync(tenant, "Aurora Grand", aurora);
        await CreatePropertyAsync(tenant, "Aurora Bay", aurora);
        await CreatePropertyAsync(tenant, "Harbourside", harbour);
        using HttpClient manager = api.Staff(tenant, "Manager", "lee");

        List<PropertyResponse>? all = await manager.GetFromJsonAsync<List<PropertyResponse>>(Route, ContractJson.Options, Cancellation);
        List<PropertyResponse>? auroras = await manager.GetFromJsonAsync<List<PropertyResponse>>($"{Route}?organizationId={aurora.Id}", ContractJson.Options, Cancellation);

        Assert.Equal(["Aurora Bay", "Aurora Grand", "Harbourside"], all?.Select(property => property.Name));
        Assert.Equal(["Aurora Bay", "Aurora Grand"], auroras?.Select(property => property.Name));
    }

    [Fact]
    public async Task Update_AsWorker_Is403_AndAuditedAsDenied()
    {
        PropertyResponse property = await CreatePropertyAsync(tenant, "Aurora Grand");
        using HttpClient worker = api.Staff(tenant, "Worker", "sam");

        using HttpResponseMessage response = await worker.PutAsJsonAsync($"{Route}/{property.Id}", new { name = "Mine now", timeZone = "Europe/London", address = strand }, Cancellation);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Contains(await api.AuditAsync(tenant, Cancellation), audit => (audit.Action, audit.Outcome, audit.Actor.Id) == ("Property.Update", AuditOutcome.Denied, "sam"));
    }

    private async Task<OrganizationResponse> CreateOrganizationAsync(TenantId owner, string name = "Aurora Hotels")
    {
        using HttpClient admin = api.Staff(owner);
        using HttpResponseMessage response = await admin.PostAsJsonAsync("/api/v1/organizations", new { name }, Cancellation);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        return (await response.Content.ReadFromJsonAsync<OrganizationResponse>(ContractJson.Options, Cancellation))!;
    }

    private async Task<PropertyResponse> CreatePropertyAsync(TenantId owner, string name, OrganizationResponse? organization = null)
    {
        organization ??= await CreateOrganizationAsync(owner);
        using HttpClient admin = api.Staff(owner);
        using HttpResponseMessage response = await admin.PostAsJsonAsync(
            Route, new { organizationId = organization.Id.Value, name, timeZone = "Europe/London", address = strand }, Cancellation);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        return (await response.Content.ReadFromJsonAsync<PropertyResponse>(ContractJson.Options, Cancellation))!;
    }

    private static async Task<string?> FirstFieldCodeAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using JsonDocument problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Cancellation));

        return problem.RootElement.GetProperty("fieldErrors")[0].GetProperty("code").GetString();
    }

    private static async Task<HttpResponseMessage> SendAsync(HttpClient client, HttpMethod method, string uri, object body, string ifMatch)
    {
        using HttpRequestMessage request = new(method, uri) { Content = JsonContent.Create(body) };
        request.Headers.TryAddWithoutValidation("If-Match", ifMatch);

        return await client.SendAsync(request, Cancellation);
    }
}
