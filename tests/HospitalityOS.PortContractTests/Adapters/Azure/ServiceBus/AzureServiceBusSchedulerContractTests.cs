using HospitalityOS.Adapters.Azure;
using HospitalityOS.PortContractTests.Scheduling;
using HospitalityOS.Ports.Bindings;
using HospitalityOS.Ports.Scheduling;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace HospitalityOS.PortContractTests.Adapters.Azure.ServiceBus;

/// <summary>
/// The Service Bus scheduler passes the scheduler contract on a queue of its own, created before first use and deleted
/// afterwards. The broker keeps real time, so the tests schedule seconds ahead and wait.
/// </summary>
public sealed class AzureServiceBusSchedulerContractTests : SchedulerContractTests
{
    private readonly ServiceBusTestNamespace bus = new();
    private readonly string queue = $"scheduled-{Guid.NewGuid():N}";

    protected override DateTimeOffset Now => DateTimeOffset.UtcNow;

    protected override TimeSpan Lead => TimeSpan.FromSeconds(6);

    protected override (IScheduler Scheduler, IAsyncDisposable Owner) CreateScheduler()
    {
        bus.SkipUnlessAvailable();

        ServiceProvider services = new ServiceCollection().AddAzureAdapters().BuildServiceProvider();
        IConfiguration settings = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionString"] = bus.ConnectionString,
                ["Queue"] = queue,
            })
            .Build();
        IAdapterFactory<IScheduler> factory = services.GetServices<IAdapterFactory<IScheduler>>().Single(candidate => candidate.AdapterName == "AzureServiceBus");
        Provisioned scheduler = new(factory.Create(services, settings), bus, queue);

        return (scheduler, new Owner(scheduler, services, bus, queue));
    }

    protected override Task PassAsync(TimeSpan time) => Task.Delay(time, TestContext.Current.CancellationToken);

    /// <summary>
    /// Creates the queue before its first use.
    /// </summary>
    private sealed class Provisioned(IScheduler inner, ServiceBusTestNamespace bus, string queue) : IScheduler
    {
        private readonly Lazy<Task> created = new(() => bus.CreateQueueAsync(queue, CancellationToken.None));

        public IScheduler Inner { get; } = inner;

        public async Task ScheduleAsync(ScheduledMessage message, CancellationToken cancellationToken)
        {
            await created.Value;
            await Inner.ScheduleAsync(message, cancellationToken);
        }

        public async Task<IAsyncDisposable> SubscribeAsync(Func<ScheduledMessage, CancellationToken, Task> deliver, CancellationToken cancellationToken)
        {
            await created.Value;

            return await Inner.SubscribeAsync(deliver, cancellationToken);
        }
    }

    private sealed class Owner(Provisioned scheduler, ServiceProvider services, ServiceBusTestNamespace bus, string queue) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            if (scheduler.Inner is IAsyncDisposable disposable)
            {
                await disposable.DisposeAsync();
            }

            await services.DisposeAsync();
            await bus.DeleteQueueAsync(queue);
        }
    }
}
