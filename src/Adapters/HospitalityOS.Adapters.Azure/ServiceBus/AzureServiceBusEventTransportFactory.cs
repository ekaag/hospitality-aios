using System.Globalization;
using HospitalityOS.Ports.Bindings;
using HospitalityOS.Ports.Eventing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace HospitalityOS.Adapters.Azure.ServiceBus;

/// <summary>
/// Binds <see cref="IEventTransport"/> to <see cref="AzureServiceBusEventTransport"/> under the adapter name
/// <c>AzureServiceBus</c>. Settings: <c>Namespace</c> or <c>ConnectionString</c> (<see cref="ServiceBusConnection"/>);
/// <c>Topic</c> (default <c>events</c>); <c>MaxDeliveryCount</c> (default 5), which the topic's subscriptions must allow.
/// </summary>
internal sealed class AzureServiceBusEventTransportFactory : IAdapterFactory<IEventTransport>
{
    public const string Name = "AzureServiceBus";

    private const string TopicSetting = "Topic";
    private const string MaxDeliveryCountSetting = "MaxDeliveryCount";
    private const string DefaultTopic = "events";
    private const int DefaultMaxDeliveryCount = 5;

    public string AdapterName => Name;

    public IEventTransport Create(IServiceProvider services, IConfiguration settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        ILogger logger = services.GetService<ILoggerFactory>()?.CreateLogger<AzureServiceBusEventTransport>() ?? NullLogger<AzureServiceBusEventTransport>.Instance;

        return new AzureServiceBusEventTransport(
            ServiceBusConnection.CreateClient(settings),
            ServiceBusConnection.EntityName(settings, TopicSetting, DefaultTopic),
            ReadMaxDeliveryCount(settings) ?? DefaultMaxDeliveryCount,
            logger);
    }

    public IEnumerable<string> ValidateSettings(IConfiguration settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        foreach (string problem in ServiceBusConnection.Validate(settings))
        {
            yield return problem;
        }

        if (settings[MaxDeliveryCountSetting] is { } count && ReadMaxDeliveryCount(settings) is not > 0)
        {
            yield return $"{MaxDeliveryCountSetting} must be a positive integer, not '{count}'.";
        }
    }

    private static int? ReadMaxDeliveryCount(IConfiguration settings) =>
        int.TryParse(settings[MaxDeliveryCountSetting], NumberStyles.Integer, CultureInfo.InvariantCulture, out int count) ? count : null;
}
