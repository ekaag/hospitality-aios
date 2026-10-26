namespace HospitalityOS.Ports.Eventing;

/// <summary>
/// A recorded event and its place in the log.
/// </summary>
/// <param name="Position">Log position, increasing in commit order; use it to page and to resume a replay.</param>
/// <param name="Message">The event.</param>
public sealed record StoredEvent(long Position, EventMessage Message);
