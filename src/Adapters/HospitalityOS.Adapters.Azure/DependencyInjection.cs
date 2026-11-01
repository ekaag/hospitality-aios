using HospitalityOS.Adapters.Azure.Secrets;
using HospitalityOS.Adapters.Azure.ServiceBus;
using HospitalityOS.Adapters.Azure.Storage;
using HospitalityOS.Ports.Bindings;
using HospitalityOS.Ports.Eventing;
using HospitalityOS.Ports.Scheduling;
using HospitalityOS.Ports.Secrets;
using HospitalityOS.Ports.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace HospitalityOS.Adapters.Azure;

/// <summary>
/// Registers the Azure adapter factories so configuration can bind ports to them.
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Registers <c>AzureServiceBus</c> for <see cref="IEventTransport"/> (a topic and its subscriptions) and for
    /// <see cref="IScheduler"/> (scheduled messages in a queue), <c>AzureBlobStorage</c> for <see cref="IBlobStore"/>, and
    /// <c>AzureKeyVault</c> for <see cref="ISecretStore"/>.
    /// </summary>
    public static IServiceCollection AddAzureAdapters(this IServiceCollection services)
    {
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IAdapterFactory<IEventTransport>, AzureServiceBusEventTransportFactory>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IAdapterFactory<IScheduler>, AzureServiceBusSchedulerFactory>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IAdapterFactory<IBlobStore>, AzureBlobStoreFactory>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IAdapterFactory<ISecretStore>, AzureKeyVaultSecretStoreFactory>());

        return services;
    }
}
