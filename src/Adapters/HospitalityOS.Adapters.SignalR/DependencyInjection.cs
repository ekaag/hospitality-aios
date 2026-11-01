using Azure.Identity;
using HospitalityOS.Ports.Bindings;
using HospitalityOS.Ports.Realtime;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Azure.SignalR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace HospitalityOS.Adapters.SignalR;

/// <summary>
/// Registers the adapters for <see cref="IRealtimeNotifier"/>: <c>SignalR</c>, SignalR in the host's own process, so
/// clients connect to the API itself; and <c>AzureSignalR</c>, the same hub with Azure SignalR Service carrying the
/// connections, so any number of API replicas reach every client. Clients negotiate with the API either way. The host maps
/// the hub with <see cref="MapRealtimeHub"/> and registers an <see cref="IRealtimeAudience"/>.
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// The port's binding key, whose default binding chooses how connections are carried.
    /// </summary>
    public const string BindingKey = "RealtimeNotifier";

    /// <summary>
    /// Registers both adapters. Which one carries connections is decided here, as SignalR is set up before the app
    /// starts: <c>AzureSignalR</c> as the default binding of <paramref name="bindings"/> (the <c>Bindings</c> section) turns
    /// the service on, at its <c>Endpoint</c> setting, signed in to as the app's identity.
    /// </summary>
    public static IServiceCollection AddSignalRAdapters(this IServiceCollection services, IConfiguration bindings)
    {
        ArgumentNullException.ThrowIfNull(bindings);

        ISignalRServerBuilder signalR = services.AddSignalR();
        IConfigurationSection binding = bindings.GetSection($"{BindingKey}:Default");

        if (string.Equals(binding["Adapter"], AzureSignalRFactory.Name, StringComparison.OrdinalIgnoreCase)
            && Uri.TryCreate(binding["Settings:Endpoint"], UriKind.Absolute, out Uri? endpoint))
        {
            signalR.AddAzureSignalR(options => options.Endpoints = [new ServiceEndpoint(endpoint, new DefaultAzureCredential())]);
        }

        services.TryAddEnumerable(ServiceDescriptor.Singleton<IAdapterFactory<IRealtimeNotifier>, SignalRRealtimeNotifierFactory>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IAdapterFactory<IRealtimeNotifier>, AzureSignalRFactory>());

        return services;
    }

    /// <summary>
    /// Maps <see cref="RealtimeHub"/> at <paramref name="pattern"/>, e.g. <c>/hubs/realtime</c>.
    /// </summary>
    public static HubEndpointConventionBuilder MapRealtimeHub(this IEndpointRouteBuilder endpoints, string pattern) =>
        endpoints.MapHub<RealtimeHub>(pattern);

    private sealed class SignalRRealtimeNotifierFactory : IAdapterFactory<IRealtimeNotifier>
    {
        public const string Name = "SignalR";

        public string AdapterName => Name;

        public IRealtimeNotifier Create(IServiceProvider services, IConfiguration settings) =>
            new SignalRRealtimeNotifier(services.GetRequiredService<IHubContext<RealtimeHub>>());

        public IEnumerable<string> ValidateSettings(IConfiguration settings) => [];
    }

    /// <summary>
    /// The same notifier: with the service on, the hub context sends through it. Settings: <c>Endpoint</c>, the service's
    /// https URL (e.g. <c>https://realtime-dev-uksouth-sigr.service.signalr.net</c>). Only the port's default binding
    /// chooses the transport; a tenant's binding to this adapter changes nothing.
    /// </summary>
    private sealed class AzureSignalRFactory : IAdapterFactory<IRealtimeNotifier>
    {
        public const string Name = "AzureSignalR";

        public string AdapterName => Name;

        public IRealtimeNotifier Create(IServiceProvider services, IConfiguration settings) =>
            new SignalRRealtimeNotifier(services.GetRequiredService<IHubContext<RealtimeHub>>());

        public IEnumerable<string> ValidateSettings(IConfiguration settings)
        {
            ArgumentNullException.ThrowIfNull(settings);

            if (!(Uri.TryCreate(settings["Endpoint"], UriKind.Absolute, out Uri? uri) && uri.Scheme == Uri.UriSchemeHttps))
            {
                yield return $"Endpoint must be the Azure SignalR Service's https URL, not '{settings["Endpoint"]}'.";
            }
        }
    }
}
