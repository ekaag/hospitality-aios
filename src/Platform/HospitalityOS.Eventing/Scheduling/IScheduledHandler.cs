namespace HospitalityOS.Eventing.Scheduling;

/// <summary>
/// Runs work scheduled through <see cref="IWorkScheduler"/> once it is due, in the tenant and flow that scheduled it,
/// acting as the agent it is registered as. Its changes commit with the record that it ran, so a redelivered message
/// runs once (P8). It re-checks the state it acts on: work whose reason has passed does nothing.
/// </summary>
/// <typeparam name="TPayload">Its input, serialized as contract JSON.</typeparam>
public interface IScheduledHandler<in TPayload>
{
    Task HandleAsync(TPayload payload, CancellationToken cancellationToken);
}
