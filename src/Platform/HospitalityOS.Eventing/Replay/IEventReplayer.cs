namespace HospitalityOS.Eventing.Replay;

/// <summary>
/// Publishes recorded events again, in log order, to every current subscription (spec §8 replay support), e.g. to
/// recover after a handler bug is fixed. Handlers that already processed an event skip it (P8), so a replay re-runs
/// only what did not happen. System-level: callers authorize it.
/// </summary>
public interface IEventReplayer
{
    /// <summary>
    /// Replays the matching events.
    /// </summary>
    /// <returns>How many events were published.</returns>
    Task<int> ReplayAsync(EventReplayRequest request, CancellationToken cancellationToken);
}
