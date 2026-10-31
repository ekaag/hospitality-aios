using System.Net;
using System.Net.Http.Json;
using HospitalityOS.Contracts.Api.DevTokens;
using HospitalityOS.Contracts.Serialization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;
using Seed = HospitalityOS.Seeding.AuroraGrand;

namespace HospitalityOS.IntegrationTests.Hosts.Api;

/// <summary>
/// Local sign-in for the frontends (J1): with dev tokens on, each seeded persona gets a token the API trusts with exactly
/// that persona's rights; off (the default), the endpoints do not exist; and on outside Development, the API will not start.
/// </summary>
public sealed class DevTokenEndpointsTests : IAsyncDisposable
{
    private readonly StaffApi api = new(seedAuroraGrand: true, configure: builder => builder.UseSetting("Authentication:DevTokens:Enabled", "true"));

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ThePersonas_AreTheSeedsPeople()
    {
        using HttpClient anonymous = api.Client();

        IReadOnlyList<DevPersona> personas = await StaffApi.OkAsync<IReadOnlyList<DevPersona>>(await anonymous.GetAsync("/api/dev/tokens/personas", Cancellation), Cancellation);

        Assert.Equal(
            [("guest-alex", "Guest"), ("worker-sam", "Worker"), ("worker-jo", "Worker"), ("worker-ravi", "Worker"), ("manager", "Manager"), ("admin", "Admin")],
            personas.Select(persona => (persona.Id, persona.Role)));
    }

    [Fact]
    public async Task AGuestToken_IsAlexsStay()
    {
        using HttpClient alex = await SignedInAsAsync("guest-alex");

        using HttpResponseMessage started = await alex.PostAsync("/api/v1/conversations", null, Cancellation);

        Assert.Equal(HttpStatusCode.Created, started.StatusCode);
        Assert.Equal(Seed.AlexStay.Value, (await started.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>(Cancellation)).GetProperty("stayContextId").GetGuid());
    }

    [Theory]
    [InlineData("worker-sam", HttpStatusCode.OK, HttpStatusCode.Forbidden)]
    [InlineData("manager", HttpStatusCode.OK, HttpStatusCode.OK)]
    [InlineData("admin", HttpStatusCode.OK, HttpStatusCode.OK)]
    [InlineData("guest-alex", HttpStatusCode.Forbidden, HttpStatusCode.Forbidden)]
    public async Task AToken_HasThePersonasRole(string persona, HttpStatusCode roster, HttpStatusCode traces)
    {
        using HttpClient client = await SignedInAsAsync(persona);

        using HttpResponseMessage readRoster = await client.GetAsync($"/api/v1/staff?propertyId={Seed.PropertyId}", Cancellation);
        using HttpResponseMessage readTraces = await client.GetAsync("/api/v1/ai/traces", Cancellation);

        Assert.Equal((roster, traces), (readRoster.StatusCode, readTraces.StatusCode));
    }

    [Fact]
    public async Task AnUnknownPersona_IsRefused()
    {
        using HttpClient anonymous = api.Client();

        using HttpResponseMessage response = await anonymous.PostAsJsonAsync("/api/dev/tokens", new IssueDevTokenRequest("ceo"), Cancellation);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(["devtokens.unknown_persona"], await StaffApi.FieldCodesAsync(response, Cancellation));
    }

    [Fact]
    public async Task Off_TheEndpointsDoNotExist()
    {
        await using StaffApi off = new();
        using HttpClient anonymous = off.Client();

        using HttpResponseMessage personas = await anonymous.GetAsync("/api/dev/tokens/personas", Cancellation);
        using HttpResponseMessage issue = await anonymous.PostAsJsonAsync("/api/dev/tokens", new IssueDevTokenRequest("admin"), Cancellation);

        Assert.Equal((HttpStatusCode.NotFound, HttpStatusCode.NotFound), (personas.StatusCode, issue.StatusCode));
    }

    [Fact]
    public void OnOutsideDevelopment_TheApiDoesNotStart()
    {
        using WebApplicationFactory<Program> factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Production");
            builder.UseSetting("Authentication:DevTokens:Enabled", "true");
        });

        InvalidOperationException refused = Assert.Throws<InvalidOperationException>(() => factory.CreateClient());

        Assert.Contains("only allowed in Development", refused.Message, StringComparison.Ordinal);
    }

    public ValueTask DisposeAsync() => api.DisposeAsync();

    private async Task<HttpClient> SignedInAsAsync(string persona)
    {
        using HttpClient anonymous = api.Client();
        HttpResponseMessage issued = await anonymous.PostAsJsonAsync("/api/dev/tokens", new IssueDevTokenRequest(persona), ContractJson.Options, Cancellation);
        DevTokenResponse token = await StaffApi.OkAsync<DevTokenResponse>(issued, Cancellation);
        Assert.Equal(persona, token.Persona.Id);

        return api.Client(token.AccessToken);
    }
}
