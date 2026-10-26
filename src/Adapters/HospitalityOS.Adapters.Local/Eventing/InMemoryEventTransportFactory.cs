using System.Globalization;
using HospitalityOS.Ports.Bindings;
using HospitalityOS.Ports.Eventing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HospitalityOS.Adapters.Local.Eventing;

/// <summary>
/// Binds <see cref="IEventTransport"/> to <see cref="InMemoryEventTransport"/> under the adapter name <c>InMemory</c>.
/// Settings: <c>MaxDeliveryCount</c> (default 5) and <c>RedeliveryDelay</c> (a <see cref="TimeSpan"/>, default 1 second).
/// </summary>
internal sealed class InMemoryEventTransportFactory : IAdapterFactory<IEventTransport>
{
    public const string Name = "InMemory";

    private const int DefaultMaxDeliveryCount = 5;
    private static readonly TimeSpan defaultRedeliveryDelay = TimeSpan.FromSeconds(1);

    public string AdapterName => Name;

    public IEventTransport Create(IServiceProvider services, IConfiguration settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        return new InMemoryEventTransport(
            ReadMaxDeliveryCount(settings) ?? DefaultMaxDeliveryCount,
            ReadRedeliveryDelay(settings) ?? defaultRedeliveryDelay,
            services.GetService<TimeProvider>() ?? TimeProvider.System);
    }

    public IEnumerable<string> ValidateSettings(IConfiguration settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        if (settings["MaxDeliveryCount"] is { } count && ReadMaxDeliveryCount(settings) is not > 0)
        {
            yield return $"MaxDeliveryCount must be a positive integer, not '{count}'.";
        }

        if (settings["RedeliveryDelay"] is { } delay && (ReadRedeliveryDelay(settings) is not { } parsed || parsed < TimeSpan.Zero))
        {
            yield return $"RedeliveryDelay must be a non-negative time span such as 00:00:01, not '{delay}'.";
        }
    }

    private static int? ReadMaxDeliveryCount(IConfiguration settings) =>
        int.TryParse(settings["MaxDeliveryCount"], NumberStyles.Integer, CultureInfo.InvariantCulture, out int count) ? count : null;

    private static TimeSpan? ReadRedeliveryDelay(IConfiguration settings) =>
        TimeSpan.TryParse(settings["RedeliveryDelay"], CultureInfo.InvariantCulture, out TimeSpan delay) ? delay : null;
}
