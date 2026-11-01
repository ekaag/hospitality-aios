using System.Security.Claims;
using HospitalityOS.Adapters.SignalR;
using HospitalityOS.Api.Authentication;
using HospitalityOS.Application.Conversations;
using HospitalityOS.Bindings;
using HospitalityOS.Ports.Realtime;
using HospitalityOS.SharedKernel.Execution;
using HospitalityOS.SharedKernel.Tenancy;
using HospitalityOS.Workflows.GuestSurface;
using HospitalityOS.Workflows.StaffSurface;
using Microsoft.AspNetCore.Authorization;

namespace HospitalityOS.Api.Realtime;

/// <summary>
/// Live updates for the staff surface (J2) and the guest surface (J3): <see cref="IRealtimeNotifier"/> is bound in
/// configuration (<c>Bindings:RealtimeNotifier</c>; <c>SignalR</c> in-process locally, <c>AzureSignalR</c> in Azure), and callers connect to
/// <see cref="HubPath"/> with their bearer token, sent as the <c>access_token</c> query value since browsers cannot set
/// headers on WebSockets. A staff member hears their tenant's staff channel; a guest hears their stay's channel and
/// nothing of the staff's. Messages name what changed, never what it is, so either reads it again through the API.
/// </summary>
internal static class RealtimeRegistration
{
    public const string HubPath = "/hubs/realtime";

    public static IServiceCollection AddRealtime(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSignalRAdapters(configuration.GetSection("Bindings"));
        services.AddPortBinding<IRealtimeNotifier>();
        services.AddSingleton<IRealtimeAudience, CallerAudience>();

        return services;
    }

    public static IEndpointRouteBuilder MapRealtime(this IEndpointRouteBuilder endpoints)
    {
        // Anyone with a valid token connects; what they hear is their audience's to decide.
        endpoints.MapRealtimeHub(HubPath).RequireAuthorization(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

        return endpoints;
    }

    private sealed class CallerAudience : IRealtimeAudience
    {
        public IReadOnlyList<string> ChannelsFor(ClaimsPrincipal caller)
        {
            if (GuestClaims.IsGuest(caller))
            {
                return GuestClaims.TryRead(caller, out TenantId stayTenant, out GuestCaller? guest, out _) ? [GuestChannel.For(stayTenant, guest!.StayContextId)] : [];
            }

            return StaffClaims.TryRead(caller, out TenantId tenant, out Actor _, out _) ? [StaffChannel.For(tenant)] : [];
        }
    }
}
