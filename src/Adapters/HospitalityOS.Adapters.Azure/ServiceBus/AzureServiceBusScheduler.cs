using Azure.Messaging.ServiceBus;
using HospitalityOS.Ports.Scheduling;
using Microsoft.Extensions.Logging;

namespace HospitalityOS.Adapters.Azure.ServiceBus;

/// <summary>
/// <see cref="IScheduler"/> on Service Bus scheduled messages in one queue: the broker holds each message until it is due,
/// so pending work survives restarts and is shared between replicas. The queue is infrastructure (IaC, P12).
/// </summary>
/// <remarks>
/// A delivery is completed once the subscriber returns. If it throws, the message is abandoned and delivered again,
/// up to the queue's maximum delivery count, after which the broker dead-letters it.
/// </remarks>
internal sealed partial class AzureServiceBusScheduler : IScheduler, IAsyncDisposable
{
    /// <summary>
    /// Scheduled handlers are short and independent, so a few run at once, as with the in-process scheduler.
    /// </summary>
    private const int ConcurrentDeliveries = 4;

    private readonly Lock gate = new();
    private readonly ServiceBusClient client;
    private readonly ServiceBusSender sender;
    private readonly string queue;
    private readonly TimeProvider timeProvider;
    private readonly ILogger logger;
    private ServiceBusProcessor? processor;

    public AzureServiceBusScheduler(ServiceBusClient client, string queue, TimeProvider timeProvider, ILogger logger)
    {
        this.client = client;
        this.queue = queue;
        this.timeProvider = timeProvider;
        this.logger = logger;
        sender = client.CreateSender(queue);
    }

    public async Task ScheduleAsync(ScheduledMessage message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);

        ServiceBusMessage sent = ServiceBusMessages.ToServiceBus(message);

        if (message.DueAt <= timeProvider.GetUtcNow())
        {
            await sender.SendMessageAsync(sent, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            await sender.ScheduleMessageAsync(sent, message.DueAt, cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task<IAsyncDisposable> SubscribeAsync(Func<ScheduledMessage, CancellationToken, Task> deliver, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(deliver);

        ServiceBusProcessor created = client.CreateProcessor(queue, new ServiceBusProcessorOptions
        {
            AutoCompleteMessages = false,
            MaxConcurrentCalls = ConcurrentDeliveries,
            PrefetchCount = 0,
        });

        lock (gate)
        {
            if (processor is not null)
            {
                throw new InvalidOperationException("The scheduler already has a subscriber.");
            }

            processor = created;
        }

        created.ProcessMessageAsync += args => DeliverAsync(deliver, args);
        created.ProcessErrorAsync += ReportAsync;

        try
        {
            await created.StartProcessingAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            await StopAsync(created).ConfigureAwait(false);

            throw;
        }

        return new Subscription(this, created);
    }

    public async ValueTask DisposeAsync()
    {
        ServiceBusProcessor? running;

        lock (gate)
        {
            running = processor;
        }

        if (running is not null)
        {
            await StopAsync(running).ConfigureAwait(false);
        }

        await sender.DisposeAsync().ConfigureAwait(false);
        await client.DisposeAsync().ConfigureAwait(false);
    }

    private static async Task DeliverAsync(Func<ScheduledMessage, CancellationToken, Task> deliver, ProcessMessageEventArgs args)
    {
        ScheduledMessage message;

        try
        {
            message = ServiceBusMessages.ToScheduled(args.Message);
        }
        catch (FormatException exception)
        {
            await args.DeadLetterMessageAsync(args.Message, "Unreadable", exception.Message, CancellationToken.None).ConfigureAwait(false);

            return;
        }

        try
        {
            await deliver(message, args.CancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !args.CancellationToken.IsCancellationRequested)
        {
            await args.AbandonMessageAsync(args.Message, cancellationToken: CancellationToken.None).ConfigureAwait(false);

            return;
        }

        await args.CompleteMessageAsync(args.Message, CancellationToken.None).ConfigureAwait(false);
    }

    private async Task StopAsync(ServiceBusProcessor stopping)
    {
        lock (gate)
        {
            if (processor == stopping)
            {
                processor = null;
            }
        }

        await stopping.StopProcessingAsync().ConfigureAwait(false);
        await stopping.DisposeAsync().ConfigureAwait(false);
    }

    private Task ReportAsync(ProcessErrorEventArgs args)
    {
        LogFailure(logger, args.Exception, queue, args.ErrorSource);

        return Task.CompletedTask;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Service Bus scheduler queue {Queue} failed to {Source}")]
    private static partial void LogFailure(ILogger logger, Exception exception, string queue, ServiceBusErrorSource source);

    private sealed class Subscription(AzureServiceBusScheduler scheduler, ServiceBusProcessor processor) : IAsyncDisposable
    {
        private int disposed;

        public ValueTask DisposeAsync() =>
            Interlocked.Exchange(ref disposed, 1) == 1 ? ValueTask.CompletedTask : new ValueTask(scheduler.StopAsync(processor));
    }
}
