namespace HospitalityOS.Ports.Eventing;

/// <summary>
/// An event a subscription could not process.
/// </summary>
/// <param name="Message">The event.</param>
/// <param name="DeliveryCount">How many times it was delivered.</param>
/// <param name="Reason">The last failure.</param>
public sealed record DeadLetteredEvent(EventMessage Message, int DeliveryCount, string Reason);
