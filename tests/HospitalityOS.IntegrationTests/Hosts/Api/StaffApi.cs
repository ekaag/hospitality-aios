using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using HospitalityOS.Capabilities.Execution;
using HospitalityOS.Contracts.Api.Organizations;
using HospitalityOS.Contracts.Api.Properties;
using HospitalityOS.Contracts.Serialization;
using HospitalityOS.Ports.Auditing;
using HospitalityOS.Ports.Eventing;
using HospitalityOS.Ports.Execution;
using HospitalityOS.Ports.Tenancy;
using HospitalityOS.SharedKernel.Execution;
using HospitalityOS.SharedKernel.Results;
using HospitalityOS.SharedKernel.Tenancy;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace HospitalityOS.IntegrationTests.Hosts.Api;

/// <summary>
/// The real API host trusting a test token issuer, as a deployment trusts its identity provider through configuration,
/// with clients that call it as a given staff member.
/// </summary>
internal sealed class StaffApi : IAsyncDisposable
{
    public const string Issuer = "https://identity.test.hospitalityos";
    public const string Audience = "hospitalityos-api";

    public static DateTimeOffset Now { get; } = new(2026, 10, 27, 9, 30, 0, TimeSpan.Zero);

    public static SymmetricSecurityKey SigningKey { get; } = new(SHA256.HashData(Encoding.UTF8.GetBytes("hospitalityos-integration-tests")));

    public StaffApi()
    {
        Factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Eventing:Dispatcher:Enabled", "false");
            builder.ConfigureTestServices(services =>
            {
                services.AddSingleton<TimeProvider>(new FakeTimeProvider(Now));
                services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
                {
                    options.TokenValidationParameters.ValidIssuer = Issuer;
                    options.TokenValidationParameters.ValidAudience = Audience;
                    options.TokenValidationParameters.IssuerSigningKey = SigningKey;
                });
            });
        });
    }

    public WebApplicationFactory<Program> Factory { get; }

    /// <summary>
    /// A token from the trusted issuer with exactly <paramref name="claims"/> (an array value becomes several claims).
    /// </summary>
    public static string Token(IDictionary<string, object> claims, SecurityKey? signingKey = null) =>
        new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = Issuer,
            Audience = Audience,
            Expires = DateTime.UtcNow.AddHours(1),
            Claims = claims,
            SigningCredentials = new SigningCredentials(signingKey ?? SigningKey, SecurityAlgorithms.HmacSha256),
        });

    public static string StaffToken(TenantId tenant, string role, string subject = "maria") =>
        Token(new Dictionary<string, object> { ["sub"] = subject, ["tenant_id"] = tenant.Value.ToString(), ["role"] = role });

    public HttpClient Client(string? token = null)
    {
        HttpClient client = Factory.CreateClient();

        if (token is not null)
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return client;
    }

    public HttpClient Staff(TenantId tenant, string role = "Admin", string subject = "maria") => Client(StaffToken(tenant, role, subject));

    public async Task<IReadOnlyList<AuditRecord>> AuditAsync(TenantId tenant, CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope = Factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().Set(tenant);
        IReadOnlyList<AuditEntry> entries = await scope.ServiceProvider.GetRequiredService<IAuditLog>().ReadAsync(new AuditQuery(), cancellationToken);

        return [.. entries.Select(entry => entry.Record)];
    }

    public async Task<IReadOnlyList<EventMessage>> EventsAsync(TenantId tenant, CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope = Factory.Services.CreateAsyncScope();
        IReadOnlyList<StoredEvent> stored = await scope.ServiceProvider.GetRequiredService<IEventStore>().ReadAsync(new EventStoreQuery(TenantId: tenant), cancellationToken);

        return [.. stored.Select(entry => entry.Message)];
    }

    /// <summary>
    /// An organization and one of its properties in <paramref name="tenant"/>, created over HTTP by an administrator.
    /// </summary>
    public async Task<PropertyResponse> CreatePropertyAsync(TenantId tenant, CancellationToken cancellationToken, string name = "Aurora Grand")
    {
        using HttpClient admin = Staff(tenant);
        using HttpResponseMessage organization = await admin.PostAsJsonAsync("/api/v1/organizations", new { name = "Aurora Hotels" }, cancellationToken);
        OrganizationResponse owner = await CreatedAsync<OrganizationResponse>(organization, cancellationToken);
        using HttpResponseMessage property = await admin.PostAsJsonAsync(
            "/api/v1/properties",
            new { organizationId = owner.Id.Value, name, timeZone = "Europe/London", address = new { line1 = "1 Strand", city = "London", countryCode = "GB" } },
            cancellationToken);

        return await CreatedAsync<PropertyResponse>(property, cancellationToken);
    }

    /// <summary>
    /// Runs a capability as a guest of <paramref name="tenant"/>, as the concierge would, and reads its output.
    /// </summary>
    public async Task<TOutput> AskAsGuestAsync<TOutput>(TenantId tenant, string capability, object input, CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope = Factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().Set(tenant);
        scope.ServiceProvider.GetRequiredService<OperationContext>().ActAs(new Actor(ActorType.Guest, "guest-1"));
        Result<CapabilityResponse> result = await scope.ServiceProvider.GetRequiredService<ICapabilityExecutor>()
            .ExecuteAsync(new CapabilityRequest(capability, JsonSerializer.SerializeToElement(input, ContractJson.Options)), cancellationToken);
        Assert.True(result.IsSuccess, result.IsFailure ? $"{result.Error.Code}: {result.Error.Message}" : null);

        return Assert.IsType<TOutput>(result.Value.Output.Deserialize<TOutput>(ContractJson.Options));
    }

    /// <summary>
    /// Asserts <c>201 Created</c> and reads the new record.
    /// </summary>
    public static async Task<T> CreatedAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        Assert.True(response.StatusCode == HttpStatusCode.Created, await response.Content.ReadAsStringAsync(cancellationToken));

        return (await response.Content.ReadFromJsonAsync<T>(ContractJson.Options, cancellationToken))!;
    }

    /// <summary>
    /// Asserts <c>200 OK</c> and reads the record.
    /// </summary>
    public static async Task<T> OkAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync(cancellationToken));

        return (await response.Content.ReadFromJsonAsync<T>(ContractJson.Options, cancellationToken))!;
    }

    /// <summary>
    /// Asserts <c>400</c> with field errors and returns their codes in order.
    /// </summary>
    public static async Task<IReadOnlyList<string?>> FieldCodesAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        string body = await response.Content.ReadAsStringAsync(cancellationToken);
        Assert.True(response.StatusCode == HttpStatusCode.BadRequest, body);
        using JsonDocument problem = JsonDocument.Parse(body);

        return [.. problem.RootElement.GetProperty("fieldErrors").EnumerateArray().Select(field => field.GetProperty("code").GetString())];
    }

    /// <summary>
    /// The problem's status and <c>code</c>.
    /// </summary>
    public static async Task<(HttpStatusCode Status, string? Code)> ProblemAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        using JsonDocument problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));

        return (response.StatusCode, problem.RootElement.GetProperty("code").GetString());
    }

    /// <summary>
    /// Sends <paramref name="body"/> (if any) with an optional <c>If-Match</c>.
    /// </summary>
    public static async Task<HttpResponseMessage> SendAsync(HttpClient client, HttpMethod method, string uri, object? body, string? ifMatch, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(client);

        using HttpRequestMessage request = new(method, uri) { Content = body is null ? null : JsonContent.Create(body) };

        if (ifMatch is not null)
        {
            request.Headers.TryAddWithoutValidation("If-Match", ifMatch);
        }

        return await client.SendAsync(request, cancellationToken);
    }

    public ValueTask DisposeAsync() => Factory.DisposeAsync();
}
