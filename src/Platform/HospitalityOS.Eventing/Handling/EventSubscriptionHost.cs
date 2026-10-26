using HospitalityOS.Ports.Bindings;
using HospitalityOS.Ports.Eventing;
using Microsoft.Extensions.Hosting;

namespace HospitalityOS.Eventing.Handling;

/// <summary>
/// Subscribes every registered handler to the bound transport when the host starts (one subscription per handler
/// name), and unsubscribes when it stops.
/// </summary>
internal sealed class EventSubscriptionHost(
    IEnumerable<EventHandlerRegistration> registrations,
    IBindingResolver<IEventTransport> transports,
    EventProcessor processor) : IHostedService
{
    private readonly Lock gate = new();
    private readonly List<IAsyncDisposable> subscriptions = [];

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        IEventTransport transport = transports.Resolve();

        foreach (EventHandlerRegistration registration in registrations)
        {
            IAsyncDisposable subscription = await transport.SubscribeAsync(
                registration.Name,
                (message, delivery) => processor.ProcessAsync(registration, message, delivery),
                cancellationToken).ConfigureAwait(false);

            lock (gate)
            {
                subscriptions.Add(subscription);
            }
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        // The host may stop services more than once, concurrently: each subscription is taken out exactly once.
        IAsyncDisposable[] stopping;

        lock (gate)
        {
            stopping = [.. subscriptions];
            subscriptions.Clear();
        }

        foreach (IAsyncDisposable subscription in stopping)
        {
            await subscription.DisposeAsync().ConfigureAwait(false);
        }
    }
}
