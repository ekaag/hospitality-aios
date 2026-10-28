using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using HospitalityOS.Contracts.Api.Properties;
using HospitalityOS.SharedKernel.Tenancy;
using Xunit;

namespace HospitalityOS.IntegrationTests.Hosts.Api;

/// <summary>
/// Requests the API cannot read are refused with a coded problem (<c>request.malformed</c>) that says where, and never
/// reach the application or fail as a server error.
/// </summary>
public sealed class MalformedRequestTests : IAsyncDisposable
{
    private readonly StaffApi api = new();
    private readonly TenantId tenant = new(Guid.CreateVersion7());

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    public ValueTask DisposeAsync() => api.DisposeAsync();

    [Fact]
    public async Task AnUnknownNameForAChoice_IsMalformed_AndNamesTheField()
    {
        PropertyResponse property = await api.CreatePropertyAsync(tenant, Cancellation);
        using HttpClient admin = api.Staff(tenant);

        using HttpResponseMessage response = await admin.PostAsJsonAsync("/api/v1/spaces", new { propertyId = property.Id.Value, name = "Keep", kind = "Castle" }, Cancellation);

        Assert.Equal((HttpStatusCode.BadRequest, "request.malformed"), await StaffApi.ProblemAsync(response, Cancellation));
        Assert.Contains("$.kind", await DetailAsync(response), StringComparison.Ordinal);
        Assert.DoesNotContain(await api.AuditAsync(tenant, Cancellation), audit => audit.Action == "Space.Create");
    }

    [Fact]
    public async Task BrokenJson_IsMalformed()
    {
        using HttpClient admin = api.Staff(tenant);
        using StringContent body = new("{\"name\": ", Encoding.UTF8, "application/json");

        using HttpResponseMessage response = await admin.PostAsync("/api/v1/organizations", body, Cancellation);

        Assert.Equal((HttpStatusCode.BadRequest, "request.malformed"), await StaffApi.ProblemAsync(response, Cancellation));
    }

    [Theory]
    [InlineData("/api/v1/organizations/00000000-0000-0000-0000-000000000000")]
    [InlineData("/api/v1/properties/00000000-0000-0000-0000-000000000000")]
    [InlineData("/api/v1/spaces/not-a-guid")]
    [InlineData("/api/v1/amenities/00000000-0000-0000-0000-000000000000")]
    [InlineData("/api/v1/policies?propertyId=00000000-0000-0000-0000-000000000000")]
    [InlineData("/api/v1/content")]
    [InlineData("/api/v1/properties?organizationId=00000000-0000-0000-0000-000000000000")]
    public async Task AnEmptyMissingOrMalformedId_IsMalformed_NotAServerError(string uri)
    {
        using HttpClient staff = api.Staff(tenant, "Worker", "sam");

        using HttpResponseMessage response = await staff.GetAsync(uri, Cancellation);

        Assert.Equal((HttpStatusCode.BadRequest, "request.malformed"), await StaffApi.ProblemAsync(response, Cancellation));
    }

    private static async Task<string?> DetailAsync(HttpResponseMessage response)
    {
        using JsonDocument problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Cancellation));

        return problem.RootElement.GetProperty("detail").GetString();
    }
}
