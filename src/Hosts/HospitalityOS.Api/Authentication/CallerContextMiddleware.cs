using HospitalityOS.Ports.Execution;
using HospitalityOS.Ports.Tenancy;
using HospitalityOS.SharedKernel.Execution;
using HospitalityOS.SharedKernel.Tenancy;

namespace HospitalityOS.Api.Authentication;

/// <summary>
/// Makes an authenticated caller the request's tenant and actor, so persistence filters on the token's tenant (P10) and
/// every event and audit record names who acted. Runs after authentication, before authorization.
/// </summary>
internal sealed class CallerContextMiddleware(RequestDelegate next)
{
    public Task InvokeAsync(HttpContext context, TenantContext tenantContext, OperationContext operation)
    {
        if (context.User.Identity?.IsAuthenticated == true && StaffClaims.TryRead(context.User, out TenantId tenant, out Actor actor, out _))
        {
            tenantContext.Set(tenant);
            operation.ActAs(actor);
        }

        return next(context);
    }
}
