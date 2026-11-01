using HospitalityOS.Ports.Bindings;
using HospitalityOS.Ports.Scheduling;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace HospitalityOS.Adapters.Azure.ServiceBus;

/// <summary>
/// Binds <see cref="IScheduler"/> to <see cref="AzureServiceBusScheduler"/> under the adapter name <c>AzureServiceBus</c>.
/// Settings: <c>Namespace</c> or <c>ConnectionString</c> (<see cref="ServiceBusConnection"/>); <c>Queue</c> (default
/// <c>scheduled</c>).
/// </summary>
internal sealed class AzureServiceBusSchedulerFactory : IAdapterFactory<IScheduler>
{
    public const string Name = "AzureServiceBus";

    private const string QueueSetting = "Queue";
    private const string DefaultQueue = "scheduled";

    public string AdapterName => Name;

    public IScheduler Create(IServiceProvider services, IConfiguration settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        ILogger logger = services.GetService<ILoggerFactory>()?.CreateLogger<AzureServiceBusScheduler>() ?? NullLogger<AzureServiceBusScheduler>.Instance;

        return new AzureServiceBusScheduler(
            ServiceBusConnection.CreateClient(settings),
            ServiceBusConnection.EntityName(settings, QueueSetting, DefaultQueue),
            services.GetService<TimeProvider>() ?? TimeProvider.System,
            logger);
    }

    public IEnumerable<string> ValidateSettings(IConfiguration settings) => ServiceBusConnection.Validate(settings);
}
