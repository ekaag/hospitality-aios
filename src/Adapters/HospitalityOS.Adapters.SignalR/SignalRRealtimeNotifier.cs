using HospitalityOS.Ports.Realtime;
using Microsoft.AspNetCore.SignalR;

namespace HospitalityOS.Adapters.SignalR;

/// <summary>
/// <see cref="IRealtimeNotifier"/> over the host's own SignalR hub (<see cref="RealtimeHub"/>): a channel is a group.
/// </summary>
internal sealed class SignalRRealtimeNotifier(IHubContext<RealtimeHub> hub) : IRealtimeNotifier
{
    public Task PublishAsync(RealtimeMessage message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);

        return hub.Clients.Group(message.Channel).SendAsync(message.Type, message.Payload, cancellationToken);
    }
}
