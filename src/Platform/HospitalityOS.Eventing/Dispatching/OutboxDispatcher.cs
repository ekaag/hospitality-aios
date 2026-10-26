using HospitalityOS.Ports.Bindings;
using HospitalityOS.Ports.Eventing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HospitalityOS.Eventing.Dispatching;

/// <summary>
/// One dispatch pass: claims due outbox events, publishes each to the bound transport, and removes it from the
/// outbox. A failed publish is retried with exponential back-off and, after <see cref="EventDispatcherOptions.MaxAttempts"/>,
/// dead-lettered; one failing event never holds up the rest (spec §37). Delivery is at-least-once (P8). Singleton.
/// </summary>
internal sealed partial class OutboxDispatcher(
    Func<DispatcherScope> createScope,
    IBindingResolver<IEventTransport> transports,
    IOptions<EventDispatcherOptions> options,
    TimeProvider timeProvider,
    ILogger<OutboxDispatcher> logger)
{
    private readonly EventDispatcherOptions settings = options.Value;

    /// <summary>
    /// Dispatches up to one batch.
    /// </summary>
    /// <returns>How many events were claimed; a full batch means more may be waiting.</returns>
    public async Task<int> DispatchOnceAsync(CancellationToken cancellationToken)
    {
        await using DispatcherScope scope = createScope();
        IReadOnlyList<OutboxEntry> due = await scope.Outbox
            .ClaimDueAsync(timeProvider.GetUtcNow(), settings.BatchSize, settings.Lease, cancellationToken)
            .ConfigureAwait(false);
        IEventTransport transport = transports.Resolve();

        foreach (OutboxEntry entry in due)
        {
            await DispatchAsync(scope.Outbox, transport, entry, cancellationToken).ConfigureAwait(false);
        }

        return due.Count;
    }

    internal TimeSpan RetryDelay(int failedAttempts)
    {
        double factor = Math.Pow(2, Math.Min(failedAttempts - 1, 30));
        double delay = Math.Min(settings.InitialRetryDelay.TotalMilliseconds * factor, settings.MaxRetryDelay.TotalMilliseconds);

        return TimeSpan.FromMilliseconds(delay);
    }

    private async Task DispatchAsync(IOutboxStore outbox, IEventTransport transport, OutboxEntry entry, CancellationToken cancellationToken)
    {
        EventMessage message = entry.Message;

        try
        {
            await transport.PublishAsync(message, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            // Any publish failure is retried or dead-lettered, never lost; the reason stays with the event.
            await RecordFailureAsync(outbox, entry, $"{exception.GetType().Name}: {exception.Message}", exception, cancellationToken).ConfigureAwait(false);

            return;
        }

        await outbox.MarkDispatchedAsync(message.EventId, cancellationToken).ConfigureAwait(false);
        LogDispatched(message.EventId, message.EventType, message.CorrelationId);
    }

    private async Task RecordFailureAsync(IOutboxStore outbox, OutboxEntry entry, string reason, Exception exception, CancellationToken cancellationToken)
    {
        EventMessage message = entry.Message;
        int failedAttempts = entry.Attempts + 1;
        DateTimeOffset now = timeProvider.GetUtcNow();

        if (failedAttempts >= settings.MaxAttempts)
        {
            await outbox.DeadLetterAsync(message.EventId, now, reason, cancellationToken).ConfigureAwait(false);
            LogDeadLettered(exception, message.EventId, message.EventType, message.CorrelationId, failedAttempts);

            return;
        }

        DateTimeOffset retryAt = now + RetryDelay(failedAttempts);
        await outbox.ScheduleRetryAsync(message.EventId, retryAt, reason, cancellationToken).ConfigureAwait(false);
        LogRetryScheduled(exception, message.EventId, message.EventType, message.CorrelationId, failedAttempts, retryAt);
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Dispatched {EventId} ({EventType}), correlation {CorrelationId}")]
    private partial void LogDispatched(Guid eventId, string eventType, Guid correlationId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Publishing {EventId} ({EventType}), correlation {CorrelationId}, failed (attempt {Attempts}); retrying at {RetryAt}")]
    private partial void LogRetryScheduled(Exception exception, Guid eventId, string eventType, Guid correlationId, int attempts, DateTimeOffset retryAt);

    [LoggerMessage(Level = LogLevel.Error, Message = "Dead-lettered {EventId} ({EventType}), correlation {CorrelationId}, after {Attempts} failed attempts")]
    private partial void LogDeadLettered(Exception exception, Guid eventId, string eventType, Guid correlationId, int attempts);
}
