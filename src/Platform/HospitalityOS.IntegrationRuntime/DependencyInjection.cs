using HospitalityOS.IntegrationContracts.Cms;
using HospitalityOS.IntegrationContracts.Crs;
using HospitalityOS.IntegrationContracts.Messaging;
using HospitalityOS.IntegrationContracts.Payments;
using HospitalityOS.IntegrationContracts.Pms;
using HospitalityOS.IntegrationContracts.Pos;
using HospitalityOS.IntegrationContracts.Providers;
using HospitalityOS.IntegrationRuntime.Configuration;
using HospitalityOS.IntegrationRuntime.Health;
using HospitalityOS.IntegrationRuntime.Providers;
using HospitalityOS.IntegrationRuntime.References;
using HospitalityOS.IntegrationRuntime.Resolution;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace HospitalityOS.IntegrationRuntime;

/// <summary>
/// Registers the integration runtime (spec §10, Epic 5): the provider registry, the per-property connector resolver,
/// configuration, health, and external references. Providers are registered by their own integration projects as
/// <see cref="IIntegrationProvider{TConnector}"/> singletons. The host must register persistence (configuration,
/// external reference, and property repositories), <see cref="TimeProvider"/>, and bind <c>IIdGenerator</c>.
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Registers <see cref="IIntegrationResolver{TConnector}"/> for every connector, <see cref="IIntegrationConfigurator"/>,
    /// <see cref="IIntegrationHealth"/>, and <see cref="IExternalReferences"/>, all scoped, over one provider registry per
    /// connector (singleton, caching connectors).
    /// </summary>
    public static IServiceCollection AddIntegrationRuntime(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton(typeof(ProviderRegistry<>));
        AddRegistry<IPmsConnector>(services);
        AddRegistry<ICmsConnector>(services);
        AddRegistry<IPosConnector>(services);
        AddRegistry<IMessagingConnector>(services);
        AddRegistry<ICrsConnector>(services);
        AddRegistry<IPaymentConnector>(services);

        services.TryAddScoped(typeof(IIntegrationResolver<>), typeof(IntegrationResolver<>));
        services.TryAddScoped<IIntegrationConfigurator, IntegrationConfigurator>();
        services.TryAddScoped<IIntegrationHealth, IntegrationHealthChecker>();
        services.TryAddScoped<IExternalReferences, ExternalReferences>();

        return services;
    }

    private static void AddRegistry<TConnector>(IServiceCollection services)
        where TConnector : class
    {
        services.AddSingleton<IProviderRegistry>(provider => provider.GetRequiredService<ProviderRegistry<TConnector>>());
    }
}
