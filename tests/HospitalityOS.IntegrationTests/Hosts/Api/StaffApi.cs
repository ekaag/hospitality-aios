using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using HospitalityOS.Ports.Auditing;
using HospitalityOS.Ports.Eventing;
using HospitalityOS.Ports.Tenancy;
using HospitalityOS.SharedKernel.Tenancy;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

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

    public ValueTask DisposeAsync() => Factory.DisposeAsync();
}
