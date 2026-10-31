using System.Collections.Concurrent;
using System.Threading.Channels;
using HospitalityOS.Ports.Eventing;

namespace HospitalityOS.Adapters.Local.Eventing;

/// <summary>
/// Delivers events within one process: one unbounded channel per subscription, processed in publish order by its
/// own loop, with redelivery and dead-lettering like a broker. Nothing survives a restart, and events published
/// while a subscription has no subscriber are not kept for it; the cloud transport (K2) keeps both.
/// </summary>
internal sealed class InMemoryEventTransport(int maxDeliveryCount, TimeSpan redeliveryDelay, TimeProvider timeProvider) : IEventTransport, IDisposable
{
    private readonly ConcurrentDictionary<string, Subscription> subscriptions = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, ConcurrentQueue<DeadLetteredEvent>> deadLetters = new(StringComparer.Ordinal);

    private int MaxDeliveryCount { get; } = maxDeliveryCount;

    private TimeSpan RedeliveryDelay { get; } = redeliveryDelay;

    private TimeProvider TimeProvider { get; } = timeProvider;

    public Task PublishAsync(EventMessage message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);
        cancellationToken.ThrowIfCancellationRequested();

        foreach (Subscription subscription in subscriptions.Values)
        {
            subscription.Enqueue(message);
        }

        return Task.CompletedTask;
    }

    public Task<IAsyncDisposable> SubscribeAsync(string subscription, Func<EventMessage, CancellationToken, Task> handler, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(subscription);
        ArgumentNullException.ThrowIfNull(handler);
        cancellationToken.ThrowIfCancellationRequested();

        Subscription created = new(this, subscription, handler);

        if (!subscriptions.TryAdd(subscription, created))
        {
            throw new InvalidOperationException($"Subscription '{subscription}' already has a subscriber.");
        }

        created.Start();

        return Task.FromResult<IAsyncDisposable>(created);
    }

    public Task<IReadOnlyList<DeadLetteredEvent>> ReadDeadLettersAsync(string subscription, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(subscription);
        cancellationToken.ThrowIfCancellationRequested();

        IReadOnlyList<DeadLetteredEvent> found = deadLetters.TryGetValue(subscription, out ConcurrentQueue<DeadLetteredEvent>? queue) ? [.. queue] : [];

        return Task.FromResult(found);
    }

    public void Dispose()
    {
        foreach (Subscription subscription in subscriptions.Values)
        {
            subscription.Stop();
        }

        subscriptions.Clear();
    }

    private void DeadLetter(string subscription, DeadLetteredEvent deadLettered) =>
        deadLetters.GetOrAdd(subscription, _ => new ConcurrentQueue<DeadLetteredEvent>()).Enqueue(deadLettered);

    private sealed class Subscription(InMemoryEventTransport transport, string name, Func<EventMessage, CancellationToken, Task> handler) : IAsyncDisposable
    {
        private readonly Channel<EventMessage> channel = Channel.CreateUnbounded<EventMessage>(new UnboundedChannelOptions { SingleReader = true });
        private readonly CancellationTokenSource stopping = new();
        private Task loop = Task.CompletedTask;

        public void Start() => loop = Task.Run(RunAsync);

        public void Enqueue(EventMessage message) => channel.Writer.TryWrite(message);

        public void Stop()
        {
            channel.Writer.TryComplete();

            // The transport stops every subscription it holds when it is disposed, which can race with the subscription
            // disposing itself; one already disposed was already stopped.
            try
            {
                stopping.Cancel();
            }
            catch (ObjectDisposedException)
            {
            }
        }

        public async ValueTask DisposeAsync()
        {
            transport.subscriptions.TryRemove(new KeyValuePair<string, Subscription>(name, this));
            Stop();

            try
            {
                await loop.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Stopping mid-delivery is expected.
            }

            stopping.Dispose();
        }

        private async Task RunAsync()
        {
            await foreach (EventMessage message in channel.Reader.ReadAllAsync(stopping.Token).ConfigureAwait(false))
            {
                await DeliverAsync(message).ConfigureAwait(false);
            }
        }

        private async Task DeliverAsync(EventMessage message)
        {
            string reason = string.Empty;

            for (int delivery = 1; delivery <= transport.MaxDeliveryCount; delivery++)
            {
                try
                {
                    await handler(message, stopping.Token).ConfigureAwait(false);

                    return;
                }
                catch (Exception exception) when (exception is not OperationCanceledException || !stopping.IsCancellationRequested)
                {
                    // Any handler failure means "redeliver", as with a broker; the reason is kept for the dead letter.
                    reason = $"{exception.GetType().Name}: {exception.Message}";
                }

                if (delivery < transport.MaxDeliveryCount && transport.RedeliveryDelay > TimeSpan.Zero)
                {
                    await Task.Delay(transport.RedeliveryDelay, transport.TimeProvider, stopping.Token).ConfigureAwait(false);
                }
            }

            transport.DeadLetter(name, new DeadLetteredEvent(message, transport.MaxDeliveryCount, reason));
        }
    }
}
