using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;

namespace HospitalityOS.Api.Authentication;

/// <summary>
/// Caller authentication: JWT bearer tokens, configured under <c>Authentication:Schemes:Bearer</c> (issuer, audiences, and
/// signing keys or an authority such as Microsoft Entra ID), so the identity provider is a configuration choice. A token
/// is either a staff token (<see cref="StaffClaims"/>) or a guest's stay token (<see cref="GuestClaims"/>); anything else
/// is refused. Locally, <c>dotnet user-jwts create --role Admin --claim tenant_id=&lt;guid&gt;</c> issues staff tokens.
/// Without configuration every token is refused.
/// </summary>
internal static class AuthenticationRegistration
{
    public static IServiceCollection AddCallerAuthentication(this IServiceCollection services)
    {
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();

        // After configuration binding, so the claim rules hold whatever the configuration says.
        services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
        {
            options.MapInboundClaims = false;
            options.TokenValidationParameters.NameClaimType = StaffClaims.Subject;
            options.TokenValidationParameters.RoleClaimType = StaffClaims.Role;
            options.Events ??= new JwtBearerEvents();
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
        services.AddAuthorization(options =>
        {
            StaffPolicies.Add(options);
            GuestPolicies.Add(options);
        });
        services.AddSingleton<IAuthorizationMiddlewareResultHandler, AuditedAuthorizationResultHandler>();

        return services;
    }
}
