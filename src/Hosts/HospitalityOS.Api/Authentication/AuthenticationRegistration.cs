using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace HospitalityOS.Api.Authentication;

/// <summary>
/// Caller authentication: JWT bearer tokens of two kinds, each from its own scheme, so the identity provider is a
/// configuration choice.
/// <list type="bullet">
/// <item><c>Authentication:Schemes:Bearer</c> (issuer, audiences, and signing keys or an authority): staff tokens
/// (<see cref="StaffClaims"/>) and guests' stay tokens (<see cref="GuestClaims"/>); anything else is refused. Locally,
/// <c>dotnet user-jwts create --role Admin --claim tenant_id=&lt;guid&gt;</c> or the dev tokens issue them.</item>
/// <item><c>Authentication:Schemes:StaffSignIn</c> (<c>Authority</c>, <c>Audience</c>, <c>ValidIssuer</c>): staff
/// signed in through Microsoft Entra External ID, who act as the access their sign-in is linked to
/// (<see cref="SignInClaims"/>).</item>
/// </list>
/// A token goes to the sign-in scheme when its issuer is that scheme's, otherwise to the bearer scheme. Without
/// configuration every token is refused.
/// </summary>
internal static class AuthenticationRegistration
{
    private const string CallerScheme = "Caller";
    private const string HubTokenQuery = "access_token";

    public static IServiceCollection AddCallerAuthentication(this IServiceCollection services)
    {
        services.AddAuthentication(CallerScheme)
            .AddJwtBearer()
            .AddJwtBearer(SignInClaims.Scheme, _ => { })
            .AddPolicyScheme(CallerScheme, CallerScheme, options => options.ForwardDefaultSelector = context =>
            {
                JwtBearerOptions signIn = context.RequestServices.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>().Get(SignInClaims.Scheme);
                return SignInClaims.IsProviderToken(TokenOf(context), signIn) ? SignInClaims.Scheme : JwtBearerDefaults.AuthenticationScheme;
            });

        // After configuration binding, so the claim rules hold whatever the configuration says.
        services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
        {
            ConfigureCommon(options);
            options.Events.OnTokenValidated = context =>
            {
                string? problem;
                bool complete = GuestClaims.IsGuest(context.Principal!)
                    ? GuestClaims.TryRead(context.Principal!, out _, out _, out problem)
                    : StaffClaims.TryRead(context.Principal!, out _, out _, out problem);

                if (!complete)
                {
                    context.Fail(problem!);
                }

                return Task.CompletedTask;
            };
        });
        services.PostConfigure<JwtBearerOptions>(SignInClaims.Scheme, options =>
        {
            ConfigureCommon(options);
            options.Events.OnTokenValidated = SignInClaims.OnTokenValidatedAsync;
        });
        services.AddOptions<JwtBearerOptions>(SignInClaims.Scheme)
            .Validate(
                options => string.IsNullOrEmpty(options.Authority) || !string.IsNullOrEmpty(options.TokenValidationParameters.ValidIssuer),
                $"Authentication:Schemes:{SignInClaims.Scheme} names an Authority but no ValidIssuer, so no token could be routed to it.")
            .ValidateOnStart();
        services.AddAuthorization(options =>
        {
            StaffPolicies.Add(options);
            GuestPolicies.Add(options);
            SignInPolicies.Add(options);
        });
        services.AddSingleton<IAuthorizationMiddlewareResultHandler, AuditedAuthorizationResultHandler>();

        return services;
    }

    private static void ConfigureCommon(JwtBearerOptions options)
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters.NameClaimType = StaffClaims.Subject;
        options.TokenValidationParameters.RoleClaimType = StaffClaims.Role;
        options.Events ??= new JwtBearerEvents();
        options.Events.OnMessageReceived = context =>
        {
            if (HubToken(context.HttpContext) is { } token)
            {
                context.Token = token;
            }

            return Task.CompletedTask;
        };
    }

    private static string? TokenOf(HttpContext context)
    {
        string authorization = context.Request.Headers.Authorization.ToString();
        return authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) ? authorization["Bearer ".Length..].Trim() : HubToken(context);
    }

    // Browsers cannot send headers on WebSockets, so the realtime hub takes the token from the query.
    private static string? HubToken(HttpContext context) =>
        context.Request.Path.StartsWithSegments(Realtime.RealtimeRegistration.HubPath) && context.Request.Query[HubTokenQuery] is { Count: > 0 } token
            ? token.ToString()
            : null;
}
