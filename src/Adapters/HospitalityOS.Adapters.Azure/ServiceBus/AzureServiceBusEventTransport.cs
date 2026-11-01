using System.Collections.Concurrent;
using Azure.Messaging.ServiceBus;
using HospitalityOS.Ports.Eventing;
using Microsoft.Extensions.Logging;

namespace HospitalityOS.Adapters.Azure.ServiceBus;

/// <summary>
/// Delivers events through one Service Bus topic: each subscription name is a durable topic subscription, so events
/// published while nobody listens wait for the next subscriber, and every replica of a host shares the work. Each
/// subscription is processed one message at a time, in the order the topic received them.
/// </summary>
/// <remarks>
/// Topic and subscriptions are infrastructure, declared in <c>deploy/events.json</c> and deployed by IaC (P12); the
/// app may send and listen but not create them, so subscribing to one that does not exist fails at once. A failed
/// delivery is abandoned for redelivery; the last allowed one is dead-lettered by the transport itself, so the dead
/// letter keeps the handler's error. The subscription's own maximum delivery count must be at least
/// <c>maxDeliveryCount</c>, and IaC sets them equal.
/// </remarks>
internal sealed partial class AzureServiceBusEventTransport : IEventTransport, IAsyncDisposable
{
    /// <summary>
    /// The application property holding how many deliveries a dead-lettered event had.
    /// </summary>
    private const string Deliveries = "deliveries";

    /// <summary>
    /// Service Bus keeps at most 4096 characters of a dead letter's description.
    /// </summary>
    private const int MaxReasonLength = 4096;

    private const int PeekPageSize = 100;

    /// <summary>
    /// Dead-letter reasons: a handler failed every allowed delivery; or the message was not an event this transport wrote.
    /// </summary>
    private const string HandlerFailed = "HandlerFailed";
    private const string Unreadable = "Unreadable";

    private readonly ConcurrentDictionary<string, Subscription> subscriptions = new(StringComparer.Ordinal);
    private readonly ServiceBusClient client;
    private readonly ServiceBusSender sender;
    private readonly string topic;
    private readonly int maxDeliveryCount;
    private readonly ILogger logger;

    public AzureServiceBusEventTransport(ServiceBusClient client, string topic, int maxDeliveryCount, ILogger logger)
    {
        this.client = client;
        this.topic = topic;
        this.maxDeliveryCount = maxDeliveryCount;
        this.logger = logger;
        sender = client.CreateSender(topic);
    }

    public Task PublishAsync(EventMessage message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);

        return sender.SendMessageAsync(ServiceBusMessages.ToServiceBus(message), cancellationToken);
    }

    public async Task<IAsyncDisposable> SubscribeAsync(string subscription, Func<EventMessage, CancellationToken, Task> handler, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(subscription);
        ArgumentNullException.ThrowIfNull(handler);

        Subscription created = new(this, subscription, handler);

        if (!subscriptions.TryAdd(subscription, created))
        {
            throw new InvalidOperationException($"Subscription '{subscription}' already has a subscriber.");
        }

        try
        {
            await EnsureExistsAsync(subscription, cancellationToken).ConfigureAwait(false);
            await created.StartAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            await created.DisposeAsync().ConfigureAwait(false);

            throw;
        }

        return created;
    }

    public async Task<IReadOnlyList<DeadLetteredEvent>> ReadDeadLettersAsync(string subscription, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(subscription);

        ServiceBusReceiver receiver = client.CreateReceiver(topic, subscription, new ServiceBusReceiverOptions { SubQueue = SubQueue.DeadLetter });

        await using (receiver.ConfigureAwait(false))
        {
            List<DeadLetteredEvent> found = [];
            long? from = null;

            while (true)
            {
                IReadOnlyList<ServiceBusReceivedMessage> page = await receiver.PeekMessagesAsync(PeekPageSize, from, cancellationToken).ConfigureAwait(false);

                if (page.Count == 0)
                {
                    return found;
                }

                foreach (ServiceBusReceivedMessage message in page)
                {
                    if (message.DeadLetterReason == Unreadable)
                    {
                        // Not an event this transport wrote, so there is no event to show; it stays in the dead letters.
                        continue;
                    }

                    int deliveries = message.ApplicationProperties.TryGetValue(Deliveries, out object? count) && count is int number ? number : message.DeliveryCount;
                    found.Add(new DeadLetteredEvent(ServiceBusMessages.ToEvent(message), deliveries, message.DeadLetterErrorDescription ?? message.DeadLetterReason ?? string.Empty));
                }

                from = page[^1].SequenceNumber + 1;
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        foreach (Subscription subscription in subscriptions.Values)
        {
            await subscription.DisposeAsync().ConfigureAwait(false);
        }

        await sender.DisposeAsync().ConfigureAwait(false);
        await client.DisposeAsync().ConfigureAwait(false);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Service Bus subscription {Topic}/{Subscription} failed to {Source}")]
    private static partial void LogFailure(ILogger logger, Exception exception, string topic, string subscription, ServiceBusErrorSource source);

    private static string Truncate(string text) => text.Length <= MaxReasonLength ? text : text[..MaxReasonLength];

    /// <summary>
    /// Fails when the subscription is not there: a processor would otherwise only report it as a background error.
    /// </summary>
    private async Task EnsureExistsAsync(string subscription, CancellationToken cancellationToken)
    {
        ServiceBusReceiver probe = client.CreateReceiver(topic, subscription);

        await using (probe.ConfigureAwait(false))
        {
            try
            {
                await probe.PeekMessageAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
            }
            catch (ServiceBusException exception) when (exception.Reason == ServiceBusFailureReason.MessagingEntityNotFound)
            {
                throw new InvalidOperationException(
                    $"Topic '{topic}' has no subscription '{subscription}'. Subscriptions are infrastructure: add it to deploy/events.json and deploy.",
                    exception);
            }
        }
    }

    private sealed class Subscription(AzureServiceBusEventTransport transport, string name, Func<EventMessage, CancellationToken, Task> handler) : IAsyncDisposable
    {
        private ServiceBusProcessor? processor;
        private int disposed;

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            processor = transport.client.CreateProcessor(transport.topic, name, new ServiceBusProcessorOptions
            {
                // Settled here, after the handler: complete, abandon for redelivery, or dead-letter.
                AutoCompleteMessages = false,
                MaxConcurrentCalls = 1,
                PrefetchCount = 0,
            });
            processor.ProcessMessageAsync += ProcessAsync;
            processor.ProcessErrorAsync += ReportAsync;
            await processor.StartProcessingAsync(cancellationToken).ConfigureAwait(false);
        }

        public async ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref disposed, 1) == 1)
            {
                return;
            }

            transport.subscriptions.TryRemove(new KeyValuePair<string, Subscription>(name, this));

            if (processor is not null)
            {
                await processor.StopProcessingAsync().ConfigureAwait(false);
                await processor.DisposeAsync().ConfigureAwait(false);
            }
        }

        private async Task ProcessAsync(ProcessMessageEventArgs args)
        {
            ServiceBusReceivedMessage received = args.Message;
            string reason;
            EventMessage message;

            try
            {
                message = ServiceBusMessages.ToEvent(received);
            }
            catch (FormatException exception)
            {
                // Redelivering a message nobody can read never helps.
                await DeadLetterAsync(args, Unreadable, $"{nameof(FormatException)}: {exception.Message}").ConfigureAwait(false);

                return;
            }

            try
            {
                await handler(message, args.CancellationToken).ConfigureAwait(false);
                await args.CompleteMessageAsync(received, CancellationToken.None).ConfigureAwait(false);

                return;
            }
            catch (OperationCanceledException) when (args.CancellationToken.IsCancellationRequested)
            {
                // Stopping mid-delivery: the lock lapses and the event is delivered again.
                throw;
            }
            catch (Exception exception)
            {
                // Any handler failure means "redeliver", as with every transport; the reason is kept for the dead letter.
                reason = $"{exception.GetType().Name}: {exception.Message}";
            }

            if (received.DeliveryCount >= transport.maxDeliveryCount)
            {
                await DeadLetterAsync(args, HandlerFailed, reason).ConfigureAwait(false);
            }
            else
            {
                await args.AbandonMessageAsync(received, cancellationToken: CancellationToken.None).ConfigureAwait(false);
            }
        }

        private static Task DeadLetterAsync(ProcessMessageEventArgs args, string code, string reason) =>
            args.DeadLetterMessageAsync(
                args.Message,
                new Dictionary<string, object> { [Deliveries] = args.Message.DeliveryCount },
                code,
                Truncate(reason),
                CancellationToken.None);

        private Task ReportAsync(ProcessErrorEventArgs args)
        {
            LogFailure(transport.logger, args.Exception, transport.topic, name, args.ErrorSource);

            return Task.CompletedTask;
        }
    }
}
