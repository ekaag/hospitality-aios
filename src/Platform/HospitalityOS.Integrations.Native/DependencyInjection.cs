using HospitalityOS.Domain.Properties.ContentItems;
using HospitalityOS.IntegrationContracts.Cms;
using HospitalityOS.IntegrationContracts.Providers;
using HospitalityOS.Integrations.Native.Cms;
using HospitalityOS.Ports.Tenancy;
using HospitalityOS.SharedKernel.Tenancy;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace HospitalityOS.Integrations.Native;

/// <summary>
/// Registers the native providers: the platform's own systems offered through the same connector interfaces as vendor
/// systems, so a property can move to a vendor, or back, by configuration alone (spec §10, Journey 18). The host must
/// register persistence and <see cref="TenantContext"/> scoped.
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Registers <c>NativeCms</c>, serving the property's published content items, as an
    /// <see cref="IIntegrationProvider{TConnector}"/> singleton.
    /// </summary>
    public static IServiceCollection AddNativeIntegrations(this IServiceCollection services)
    {
        // A connector outlives requests, so each read gets its own scope acting for the connector's tenant; the factory
        // lives here so no connector holds the container (P15).
        services.TryAddSingleton<Func<TenantId, NativeContentScope>>(provider => tenantId =>
        {
            AsyncServiceScope scope = provider.CreateAsyncScope();
            scope.ServiceProvider.GetRequiredService<TenantContext>().Set(tenantId);

            return new NativeContentScope(scope.ServiceProvider.GetRequiredService<IContentItemRepository>(), scope);
        });
        services.AddSingleton<IIntegrationProvider<ICmsConnector>, NativeCmsProvider>();

        return services;
    }
}
