namespace HospitalityOS.Ports.Eventing;

/// <summary>
/// A queued event and its publish history.
/// </summary>
/// <param name="Message">The event.</param>
/// <param name="Attempts">Failed publish attempts so far.</param>
/// <param name="LastError">Why the last attempt failed, if one did.</param>
public sealed record OutboxEntry(EventMessage Message, int Attempts, string? LastError);
