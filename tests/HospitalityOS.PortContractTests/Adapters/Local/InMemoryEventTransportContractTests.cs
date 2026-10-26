using HospitalityOS.Adapters.Local;
using HospitalityOS.PortContractTests.Eventing;
using HospitalityOS.Ports.Bindings;
using HospitalityOS.Ports.Eventing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HospitalityOS.PortContractTests.Adapters.Local;

/// <summary>
/// The in-memory transport, created the way bindings create it: through its registered adapter factory.
/// </summary>
public sealed class InMemoryEventTransportContractTests : EventTransportContractTests
{
    protected override (IEventTransport Transport, IDisposable Owner) CreateTransport()
    {
        ServiceProvider services = new ServiceCollection().AddLocalAdapters().BuildServiceProvider();
        IConfiguration settings = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["MaxDeliveryCount"] = MaxDeliveryCount.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["RedeliveryDelay"] = "00:00:00",
            })
            .Build();
        IAdapterFactory<IEventTransport> factory = services.GetServices<IAdapterFactory<IEventTransport>>().Single(candidate => candidate.AdapterName == "InMemory");
        IEventTransport transport = factory.Create(services, settings);

        return (transport, new Owner(transport, services));
    }

    private sealed class Owner(IEventTransport transport, ServiceProvider services) : IDisposable
    {
        public void Dispose()
        {
            (transport as IDisposable)?.Dispose();
            services.Dispose();
        }
    }
}
