using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;

namespace HospitalityOS.Api.Authentication;

/// <summary>
/// Staff authentication: JWT bearer tokens, configured under <c>Authentication:Schemes:Bearer</c> (issuer, audiences, and
/// signing keys or an authority such as Microsoft Entra ID), so the identity provider is a configuration choice. Locally,
/// <c>dotnet user-jwts create --role Admin --claim tenant_id=&lt;guid&gt;</c> issues tokens. Without configuration every
/// token is refused.
/// </summary>
internal static class AuthenticationRegistration
{
    public static IServiceCollection AddStaffAuthentication(this IServiceCollection services)
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
                if (!StaffClaims.TryRead(context.Principal!, out _, out _, out string? problem))
                {
                    context.Fail(problem!);
                }

                return Task.CompletedTask;
            };
        });
        services.AddAuthorization(StaffPolicies.Add);
        services.AddSingleton<IAuthorizationMiddlewareResultHandler, AuditedAuthorizationResultHandler>();

        return services;
    }
}
