namespace HospitalityOS.Ports.Realtime;

/// <summary>
/// Pushes messages to connected clients (the plan's <c>IRealtimeNotifier</c>: in-process SignalR locally, Azure SignalR
/// later), so the staff surface hears about work as it changes instead of polling. Delivery is best effort and only to
/// clients connected now: a message says that something changed, and clients read what changed through the API, which
/// authorizes them.
/// </summary>
public interface IRealtimeNotifier
{
    /// <summary>
    /// Sends <paramref name="message"/> to every client listening on its channel.
    /// </summary>
    Task PublishAsync(RealtimeMessage message, CancellationToken cancellationToken);
}
