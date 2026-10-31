using HospitalityOS.Ports.Scheduling;

namespace HospitalityOS.Adapters.Local.Scheduling;

/// <summary>
/// <see cref="IScheduler"/> on timers of the injected <see cref="TimeProvider"/>, for local runs and tests. Messages live
/// in memory, so a restart loses those not yet due; a durable scheduler (Service Bus scheduled messages, K2) keeps them.
/// </summary>
internal sealed class InProcessScheduler(TimeProvider timeProvider) : IScheduler, IDisposable
{
    private readonly Lock gate = new();
    private readonly HashSet<ITimer> timers = [];
    private readonly List<ScheduledMessage> waiting = [];
    private Func<ScheduledMessage, CancellationToken, Task>? subscriber;
    private bool disposed;

    public Task ScheduleAsync(ScheduledMessage message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);

        TimeSpan delay = message.DueAt - timeProvider.GetUtcNow();

        lock (gate)
        {
            ObjectDisposedException.ThrowIf(disposed, this);

            ITimer? timer = null;
            timer = timeProvider.CreateTimer(_ => Fire(message, timer!), null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
            timers.Add(timer);
            timer.Change(delay > TimeSpan.Zero ? delay : TimeSpan.Zero, Timeout.InfiniteTimeSpan);
        }

        return Task.CompletedTask;
    }

    public Task<IAsyncDisposable> SubscribeAsync(Func<ScheduledMessage, CancellationToken, Task> deliver, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(deliver);

        ScheduledMessage[] due;

        lock (gate)
        {
            if (subscriber is not null)
            {
                throw new InvalidOperationException("The scheduler already has a subscriber.");
            }

            subscriber = deliver;
            due = [.. waiting];
            waiting.Clear();
        }

        foreach (ScheduledMessage message in due)
        {
            Deliver(deliver, message);
        }

        return Task.FromResult<IAsyncDisposable>(new Subscription(this, deliver));
    }

    public void Dispose()
    {
        lock (gate)
        {
            disposed = true;

            foreach (ITimer timer in timers)
            {
                timer.Dispose();
            }

            timers.Clear();
        }
    }

    private void Fire(ScheduledMessage message, ITimer timer)
    {
        Func<ScheduledMessage, CancellationToken, Task>? deliver;

        lock (gate)
        {
            if (!timers.Remove(timer))
            {
                return;
            }

            timer.Dispose();
            deliver = subscriber;

            if (deliver is null)
            {
                waiting.Add(message);

                return;
            }
        }

        Deliver(deliver, message);
    }

    // The subscriber handles and logs its own failures; the scheduler only hands messages over.
    private static void Deliver(Func<ScheduledMessage, CancellationToken, Task> deliver, ScheduledMessage message) =>
        _ = Task.Run(() => deliver(message, CancellationToken.None));

    private sealed class Subscription(InProcessScheduler scheduler, Func<ScheduledMessage, CancellationToken, Task> deliver) : IAsyncDisposable
    {
        public ValueTask DisposeAsync()
        {
            lock (scheduler.gate)
            {
                if (scheduler.subscriber == deliver)
                {
                    scheduler.subscriber = null;
                }
            }

            return ValueTask.CompletedTask;
        }
    }
}
