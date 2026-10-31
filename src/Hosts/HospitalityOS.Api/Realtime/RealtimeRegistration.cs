using System.Security.Claims;
using HospitalityOS.Adapters.SignalR;
using HospitalityOS.Api.Authentication;
using HospitalityOS.Bindings;
using HospitalityOS.Ports.Realtime;
using HospitalityOS.SharedKernel.Execution;
using HospitalityOS.SharedKernel.Tenancy;
using HospitalityOS.Workflows.StaffSurface;

namespace HospitalityOS.Api.Realtime;

/// <summary>
/// Live updates for the staff surface (J2): <see cref="IRealtimeNotifier"/> is bound in configuration
/// (<c>Bindings:RealtimeNotifier</c>; <c>SignalR</c> in-process locally), and staff connect to <see cref="HubPath"/> with
/// their bearer token, sent as the <c>access_token</c> query value since browsers cannot set headers on WebSockets. A staff
/// member hears their tenant's staff channel; anyone else hears nothing.
/// </summary>
internal static class RealtimeRegistration
{
    public const string HubPath = "/hubs/realtime";

    public static IServiceCollection AddRealtime(this IServiceCollection services)
    {
        services.AddSignalRAdapters();
        services.AddPortBinding<IRealtimeNotifier>();
        services.AddSingleton<IRealtimeAudience, StaffAudience>();

        return services;
    }

    public static IEndpointRouteBuilder MapRealtime(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapRealtimeHub(HubPath).RequireAuthorization(StaffPolicies.Staff);

        return endpoints;
    }

    private sealed class StaffAudience : IRealtimeAudience
    {
        public IReadOnlyList<string> ChannelsFor(ClaimsPrincipal caller) =>
            !GuestClaims.IsGuest(caller) && StaffClaims.TryRead(caller, out TenantId tenant, out Actor _, out _)
                ? [StaffChannel.For(tenant)]
                : [];
    }
}
