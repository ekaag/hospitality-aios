namespace HospitalityOS.Ports.Eventing;

/// <summary>
/// A recorded event the dispatcher gave up publishing (spec §37).
/// </summary>
/// <param name="Message">The event.</param>
/// <param name="Attempts">How many publishes failed.</param>
/// <param name="Error">The last failure.</param>
/// <param name="DeadLetteredAt">When it was given up on.</param>
public sealed record DeadLetteredOutboxEvent(EventMessage Message, int Attempts, string Error, DateTimeOffset DeadLetteredAt);
