using System.Security.Cryptography;
using HospitalityOS.Api.Http;
using HospitalityOS.Contracts.Api.DevTokens;
using HospitalityOS.SharedKernel.Results;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace HospitalityOS.Api.Authentication;

/// <summary>
/// Local sign-in for the frontends (J1): with <c>Authentication:DevTokens:Enabled</c>, <c>/api/dev/tokens</c> issues
/// bearer tokens for the <see cref="DevPersonas"/>, signed with a key made when the process starts (so they stop working
/// when it restarts, and no key is ever configured or committed) and trusted by the bearer scheme alongside whatever it
/// is configured with. Only in Development: enabled anywhere else, the API refuses to start. The frontends only send the
/// token, so a real identity provider replaces this by configuration.
/// </summary>
internal static class DevTokens
{
    public const string SectionName = "Authentication:DevTokens";
    public const string Route = "/api/dev/tokens";
    public const string Issuer = "https://dev-tokens.hospitalityos.local";
    public const string Audience = "hospitalityos-api";

    public const string UnknownPersonaCode = "devtokens.unknown_persona";

    public static IServiceCollection AddDevTokens(this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(environment);

        DevTokenOptions options = configuration.GetSection(SectionName).Get<DevTokenOptions>() ?? new DevTokenOptions();

        if (!options.Enabled)
        {
            return services;
        }

        if (!environment.IsDevelopment())
        {
            throw new InvalidOperationException(
                $"{SectionName}:Enabled is only allowed in Development; this is {environment.EnvironmentName}. Anyone could sign in as anyone.");
        }

        DevTokenIssuer issuer = new(new SymmetricSecurityKey(RandomNumberGenerator.GetBytes(32)), options.Lifetime);
        services.AddSingleton(issuer);
        services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, bearer =>
        {
            TokenValidationParameters parameters = bearer.TokenValidationParameters;
            parameters.ValidIssuers = [.. parameters.ValidIssuers ?? [], .. parameters.ValidIssuer is { } configured ? [configured] : Array.Empty<string>(), Issuer];
            parameters.ValidAudiences = [.. parameters.ValidAudiences ?? [], .. parameters.ValidAudience is { } audience ? [audience] : Array.Empty<string>(), Audience];
            parameters.IssuerSigningKeys = [.. parameters.IssuerSigningKeys ?? [], .. parameters.IssuerSigningKey is { } key ? [key] : Array.Empty<SecurityKey>(), issuer.SigningKey];
        });

        return services;
    }

    /// <summary>
    /// Maps the endpoints when dev tokens are on.
    /// </summary>
    public static IEndpointRouteBuilder MapDevTokenEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        if (endpoints.ServiceProvider.GetService<DevTokenIssuer>() is null)
        {
            return endpoints;
        }

        RouteGroupBuilder group = endpoints.MapGroup(Route).AllowAnonymous().WithTags("DevTokens");

        group.MapGet("/personas", () => TypedResults.Ok(DevPersonas.All.Select(persona => persona.Persona).ToList())).WithName("DevToken.ListPersonas");
        group.MapPost("/", Issue).Produces<DevTokenResponse>().WithName("DevToken.Issue");

        return endpoints;
    }

    private static IResult Issue(IssueDevTokenRequest request, DevTokenIssuer issuer) =>
        DevPersonas.Find(request.Persona) is { } persona
            ? TypedResults.Ok(issuer.Issue(persona))
            : Error.Validation("persona", UnknownPersonaCode, $"There is no dev persona '{request.Persona}'.").ToProblem();
}

/// <summary>
/// <c>Authentication:DevTokens</c>.
/// </summary>
internal sealed class DevTokenOptions
{
    /// <summary>
    /// Whether dev tokens are issued and trusted; Development only.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// How long a token works.
    /// </summary>
    public TimeSpan Lifetime { get; set; } = TimeSpan.FromHours(8);
}

/// <summary>
/// Signs dev tokens with the process's key.
/// </summary>
internal sealed class DevTokenIssuer(SecurityKey signingKey, TimeSpan lifetime)
{
    public SecurityKey SigningKey { get; } = signingKey;

    public DevTokenResponse Issue(DevPersonaClaims persona)
    {
        ArgumentNullException.ThrowIfNull(persona);

        // The bearer handler checks lifetimes against the system clock, so tokens are issued by it too.
        DateTime now = TimeProvider.System.GetUtcNow().UtcDateTime;
        DateTime expires = now.Add(lifetime);
        string token = new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = DevTokens.Issuer,
            Audience = DevTokens.Audience,
            IssuedAt = now,
            NotBefore = now,
            Expires = expires,
            Claims = persona.Claims.ToDictionary(),
            SigningCredentials = new SigningCredentials(SigningKey, SecurityAlgorithms.HmacSha256),
        });

        return new DevTokenResponse(token, new DateTimeOffset(expires, TimeSpan.Zero), persona.Persona);
    }
}
