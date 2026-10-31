using HospitalityOS.Ports.Realtime;
using Microsoft.AspNetCore.SignalR;

namespace HospitalityOS.Adapters.SignalR;

/// <summary>
/// The SignalR hub clients connect to. Clients only listen: on connecting, each joins the channels
/// <see cref="IRealtimeAudience"/> allows its caller, one SignalR group per channel, and messages arrive as the
/// <see cref="RealtimeMessage.Type"/> method with the payload as its argument.
/// </summary>
internal sealed class RealtimeHub(IRealtimeAudience audience) : Hub
{
    public override async Task OnConnectedAsync()
    {
        if (Context.User is { } caller)
        {
            foreach (string channel in audience.ChannelsFor(caller))
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, channel, Context.ConnectionAborted).ConfigureAwait(false);
            }
        }

        await base.OnConnectedAsync().ConfigureAwait(false);
    }
}
