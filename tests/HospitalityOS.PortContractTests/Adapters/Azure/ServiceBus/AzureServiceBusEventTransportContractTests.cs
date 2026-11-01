using System.Globalization;
using HospitalityOS.Adapters.Azure;
using HospitalityOS.PortContractTests.Eventing;
using HospitalityOS.Ports.Bindings;
using HospitalityOS.Ports.Eventing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HospitalityOS.PortContractTests.Adapters.Azure.ServiceBus;

/// <summary>
/// The Service Bus transport, created the way bindings create it, on a topic of its own. The topic and each
/// subscription are created just before first use, as IaC would have created them, then deleted.
/// </summary>
public sealed class AzureServiceBusEventTransportContractTests : EventTransportContractTests
{
    private readonly ServiceBusTestNamespace bus = new();
    private readonly string topic = $"contract-{Guid.NewGuid():N}";

    protected override (IEventTransport Transport, IAsyncDisposable Owner) CreateTransport()
    {
        bus.SkipUnlessAvailable();

        ServiceProvider services = new ServiceCollection().AddAzureAdapters().BuildServiceProvider();
        IConfiguration settings = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionString"] = bus.ConnectionString,
                ["Topic"] = topic,
                ["MaxDeliveryCount"] = MaxDeliveryCount.ToString(CultureInfo.InvariantCulture),
            })
            .Build();
        IAdapterFactory<IEventTransport> factory = services.GetServices<IAdapterFactory<IEventTransport>>().Single(candidate => candidate.AdapterName == "AzureServiceBus");
        Provisioned transport = new(factory.Create(services, settings), bus, topic);

        return (transport, new Owner(transport, services, bus, topic));
    }

    /// <summary>
    /// Creates the topic before its first use and each subscription before it is subscribed to.
    /// </summary>
    private sealed class Provisioned(IEventTransport inner, ServiceBusTestNamespace bus, string topic) : IEventTransport
    {
        private readonly Lazy<Task> created = new(() => bus.CreateTopicAsync(topic, CancellationToken.None));

        public IEventTransport Inner { get; } = inner;

        public async Task PublishAsync(EventMessage message, CancellationToken cancellationToken)
        {
            await created.Value;
            await Inner.PublishAsync(message, cancellationToken);
        }

        public async Task<IAsyncDisposable> SubscribeAsync(string subscription, Func<EventMessage, CancellationToken, Task> handler, CancellationToken cancellationToken)
        {
            await created.Value;
            await bus.EnsureSubscriptionAsync(topic, subscription, cancellationToken);

            return await Inner.SubscribeAsync(subscription, handler, cancellationToken);
        }

        public async Task<IReadOnlyList<DeadLetteredEvent>> ReadDeadLettersAsync(string subscription, CancellationToken cancellationToken)
        {
            await created.Value;

            return await Inner.ReadDeadLettersAsync(subscription, cancellationToken);
        }

        public async Task<bool> RemoveDeadLetterAsync(string subscription, Guid eventId, CancellationToken cancellationToken)
        {
            await created.Value;

            return await Inner.RemoveDeadLetterAsync(subscription, eventId, cancellationToken);
        }
    }

    private sealed class Owner(Provisioned transport, ServiceProvider services, ServiceBusTestNamespace bus, string topic) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            if (transport.Inner is IAsyncDisposable disposable)
            {
                await disposable.DisposeAsync();
            }

            await services.DisposeAsync();
            await bus.DeleteTopicAsync(topic);
        }
    }
}
