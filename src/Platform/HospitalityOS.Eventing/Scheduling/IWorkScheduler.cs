namespace HospitalityOS.Eventing.Scheduling;

/// <summary>
/// Schedules work for a registered <see cref="IScheduledHandler{TPayload}"/>, in the current tenant and flow (the
/// current event, if any, is its cause). Scoped. Scheduling takes effect at once, not with the caller's commit: a
/// handler must re-check state anyway, so work scheduled by a change that did not commit does nothing.
/// </summary>
public interface IWorkScheduler
{
    /// <summary>
    /// Runs <paramref name="handler"/> with <paramref name="payload"/> at <paramref name="dueAt"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException">No scheduled handler is registered as <paramref name="handler"/>.</exception>
    Task ScheduleAsync<TPayload>(string handler, TPayload payload, DateTimeOffset dueAt, CancellationToken cancellationToken);
}
