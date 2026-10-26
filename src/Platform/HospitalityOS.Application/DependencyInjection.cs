using HospitalityOS.Application.Auditing;
using HospitalityOS.Ports.Execution;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace HospitalityOS.Application;

/// <summary>
/// Registers the application services. The host must register persistence (the audit log), <c>TenantContext</c>,
/// and bind <c>IIdGenerator</c>.
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Registers <see cref="IAuditTrail"/> and the scope's <see cref="OperationContext"/>.
    /// </summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddScoped<OperationContext>();
        services.TryAddScoped<IAuditTrail, AuditTrail>();

        return services;
    }
}
