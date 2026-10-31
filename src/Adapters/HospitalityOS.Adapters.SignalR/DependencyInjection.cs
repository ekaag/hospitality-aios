using HospitalityOS.Ports.Bindings;
using HospitalityOS.Ports.Realtime;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace HospitalityOS.Adapters.SignalR;

/// <summary>
/// Registers the <c>SignalR</c> adapter for <see cref="IRealtimeNotifier"/>: SignalR in the host's own process, so clients
/// connect to the API itself. The host maps the hub with <see cref="MapRealtimeHub"/> and registers an
/// <see cref="IRealtimeAudience"/>.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddSignalRAdapters(this IServiceCollection services)
    {
        services.AddSignalR();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IAdapterFactory<IRealtimeNotifier>, SignalRRealtimeNotifierFactory>());

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
}
