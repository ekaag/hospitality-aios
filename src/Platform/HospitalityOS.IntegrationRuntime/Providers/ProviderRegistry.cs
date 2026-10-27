using System.Collections.Frozen;
using HospitalityOS.Domain.Integrations.IntegrationConfigurations;
using HospitalityOS.IntegrationContracts.Providers;
using HospitalityOS.Ports.Bindings;

namespace HospitalityOS.IntegrationRuntime.Providers;

/// <summary>
/// The registered providers of <typeparamref name="TConnector"/> (spec "provider registry"), and the connectors they
/// created, one per configuration, replaced when the configuration's version changes. Singleton.
/// </summary>
/// <remarks>
/// A replaced connector is not disposed: requests that resolved it may still be using it.
/// </remarks>
internal sealed class ProviderRegistry<TConnector> : IProviderRegistry
    where TConnector : class
{
    private readonly Dictionary<string, IIntegrationProvider<TConnector>> providers = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<IntegrationConfigurationId, CachedConnector> connectors = [];
    private readonly Lock gate = new();

    public ProviderRegistry(IEnumerable<IIntegrationProvider<TConnector>> providers)
    {
        ArgumentNullException.ThrowIfNull(providers);

        foreach (IIntegrationProvider<TConnector> provider in providers)
        {
            if (!this.providers.TryAdd(provider.Name, provider))
            {
                throw new InvalidOperationException($"Two {typeof(TConnector).Name} providers are named '{provider.Name}'.");
            }
        }

        ProviderNames = [.. this.providers.Values.Select(provider => provider.Name).Order(StringComparer.OrdinalIgnoreCase)];
    }

    public ConnectorKind Kind { get; } = ConnectorKinds.Of<TConnector>();

    public IReadOnlyList<string> ProviderNames { get; }

    public string? FindProviderName(string name) => providers.GetValueOrDefault(name)?.Name;

    public IReadOnlyList<string> ValidateSettings(string providerName, IReadOnlyDictionary<string, string> settings) =>
        [.. providers[providerName].ValidateSettings(settings)];

    /// <summary>
    /// The configuration's connector, created by its provider on first use and after each change; <c>null</c> when the
    /// provider is not registered.
    /// </summary>
    public TConnector? GetConnector(IntegrationConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        lock (gate)
        {
            if (connectors.TryGetValue(configuration.Id, out CachedConnector? cached) && cached.Version == configuration.Version)
            {
                return cached.Connector;
            }

            if (!providers.TryGetValue(configuration.ProviderName, out IIntegrationProvider<TConnector>? provider))
            {
                return null;
            }

            TConnector connector = provider.Create(new ConnectorContext(
                configuration.TenantId,
                configuration.PropertyId,
                configuration.Settings.ToFrozenDictionary(setting => setting.Name, setting => setting.Value, StringComparer.OrdinalIgnoreCase)));
            connectors[configuration.Id] = new CachedConnector(configuration.Version, connector);

            return connector;
        }
    }

    public async Task<AdapterHealth> CheckHealthAsync(IntegrationConfiguration configuration, CancellationToken cancellationToken)
    {
        try
        {
            TConnector? connector = GetConnector(configuration);

            if (connector is null)
            {
                return AdapterHealth.Unhealthy(IntegrationRuntimeErrors.ProviderUnavailable(Kind, configuration.ProviderName).Message);
            }

            return connector is IAdapterHealthProbe probe
                ? await probe.CheckHealthAsync(cancellationToken).ConfigureAwait(false)
                : AdapterHealth.Healthy;
        }
#pragma warning disable CA1031 // A health check reports any provider failure instead of throwing.
        catch (Exception exception) when (exception is not OperationCanceledException)
#pragma warning restore CA1031
        {
            return AdapterHealth.Unhealthy(exception.Message);
        }
    }

    private sealed record CachedConnector(long Version, TConnector Connector);
}
