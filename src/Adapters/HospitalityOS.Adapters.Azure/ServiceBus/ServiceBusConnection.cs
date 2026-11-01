using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Configuration;

namespace HospitalityOS.Adapters.Azure.ServiceBus;

/// <summary>
/// How a Service Bus adapter reaches its namespace, from the binding's settings: <c>Namespace</c>, the fully qualified
/// namespace (e.g. <c>events-dev-uksouth-sbns.servicebus.windows.net</c>), signed in to as the app's own identity with no
/// stored credentials; or <c>ConnectionString</c>, for the emulator and local runs. Exactly one of the two.
/// </summary>
internal static class ServiceBusConnection
{
    public const string NamespaceSetting = "Namespace";
    public const string ConnectionStringSetting = "ConnectionString";

    public static IEnumerable<string> Validate(IConfiguration settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        bool hasNamespace = !string.IsNullOrWhiteSpace(settings[NamespaceSetting]);
        bool hasConnectionString = !string.IsNullOrWhiteSpace(settings[ConnectionStringSetting]);

        if (hasNamespace == hasConnectionString)
        {
            yield return $"Set exactly one of {NamespaceSetting} (signs in as the app's identity) and {ConnectionStringSetting} (the emulator, local runs).";
        }
    }

    public static ServiceBusClient CreateClient(IConfiguration settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        if (settings[ConnectionStringSetting] is { Length: > 0 } connectionString)
        {
            return new ServiceBusClient(connectionString);
        }

        string fullyQualifiedNamespace = settings[NamespaceSetting]
            ?? throw new InvalidOperationException($"Set {NamespaceSetting} or {ConnectionStringSetting}.");

        return new ServiceBusClient(fullyQualifiedNamespace, AzureCredentials.App);
    }

    /// <summary>
    /// An entity name setting, or its default.
    /// </summary>
    public static string EntityName(IConfiguration settings, string setting, string fallback) =>
        settings[setting] is { Length: > 0 } name ? name : fallback;
}
