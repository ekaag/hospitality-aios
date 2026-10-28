using HospitalityOS.Application.Administration;
using HospitalityOS.Application.Auditing;
using HospitalityOS.Application.Organizations;
using HospitalityOS.Application.Properties;
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
    /// Registers <see cref="IAuditTrail"/>, the scope's <see cref="OperationContext"/>, and the administration
    /// services (<see cref="IOrganizationAdministration"/>, <see cref="IPropertyAdministration"/>).
    /// </summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddScoped<OperationContext>();
        services.TryAddScoped<IAuditTrail, AuditTrail>();
        services.TryAddScoped<AuditedCommit>();
        services.TryAddScoped<IOrganizationAdministration, OrganizationAdministration>();
        services.TryAddScoped<IPropertyAdministration, PropertyAdministration>();

        return services;
    }
}
