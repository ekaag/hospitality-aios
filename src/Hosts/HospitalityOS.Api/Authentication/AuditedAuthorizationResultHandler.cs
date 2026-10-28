using HospitalityOS.Application.Auditing;
using HospitalityOS.Ports.Auditing;
using HospitalityOS.Ports.Persistence;
using HospitalityOS.Ports.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;

namespace HospitalityOS.Api.Authentication;

/// <summary>
/// Audits a known caller refused by role (spec §37: denied attempts are audit history), then answers as usual (403).
/// Unauthenticated requests get 401 unaudited: there is no tenant to record them in.
/// </summary>
internal sealed class AuditedAuthorizationResultHandler : IAuthorizationMiddlewareResultHandler
{
    private readonly AuthorizationMiddlewareResultHandler standard = new();

    public async Task HandleAsync(RequestDelegate next, HttpContext context, AuthorizationPolicy policy, PolicyAuthorizationResult authorizeResult)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(authorizeResult);

        if (authorizeResult.Forbidden && context.RequestServices.GetRequiredService<TenantContext>().IsSet)
        {
            string action = context.GetEndpoint()?.Metadata.GetMetadata<IEndpointNameMetadata>()?.EndpointName ?? $"{context.Request.Method} {context.Request.Path}";
            context.RequestServices.GetRequiredService<IAuditTrail>().Record(action, AuditOutcome.Denied, detail: "role");
            await context.RequestServices.GetRequiredService<IUnitOfWork>().CommitAsync(context.RequestAborted);
        }

        await standard.HandleAsync(next, context, policy, authorizeResult);
    }
}
