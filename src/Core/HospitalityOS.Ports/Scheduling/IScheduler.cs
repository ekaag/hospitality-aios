namespace HospitalityOS.Ports.Scheduling;

/// <summary>
/// Delayed work (SLA expiry, timeouts, retries): holds messages until they are due, then delivers them to the
/// subscriber. Delivery is at least once and may be late, never early; handlers re-check the state they act on, so a
/// message whose reason has passed does nothing.
/// </summary>
public interface IScheduler
{
    /// <summary>
    /// Holds <paramref name="message"/> until <see cref="ScheduledMessage.DueAt"/>.
    /// </summary>
    Task ScheduleAsync(ScheduledMessage message, CancellationToken cancellationToken);

    /// <summary>
    /// Delivers due messages to <paramref name="deliver"/>, including any that fell due before subscribing, until the
    /// returned subscription is disposed. One subscriber at a time.
    /// </summary>
    Task<IAsyncDisposable> SubscribeAsync(Func<ScheduledMessage, CancellationToken, Task> deliver, CancellationToken cancellationToken);
}
