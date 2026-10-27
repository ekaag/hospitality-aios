using HospitalityOS.Domain.Integrations.IntegrationConfigurations;
using HospitalityOS.Ports.Bindings;

namespace HospitalityOS.IntegrationRuntime.Providers;

/// <summary>
/// One connector kind's registered providers, without the connector type, for configuration and health.
/// </summary>
internal interface IProviderRegistry
{
    ConnectorKind Kind { get; }

    /// <summary>
    /// The registered providers' names, ordered.
    /// </summary>
    IReadOnlyList<string> ProviderNames { get; }

    /// <summary>
    /// The registered spelling of a provider name matched ignoring case, or <c>null</c> when none matches.
    /// </summary>
    string? FindProviderName(string name);

    /// <summary>
    /// The provider's problems with the settings; empty when they are valid.
    /// </summary>
    IReadOnlyList<string> ValidateSettings(string providerName, IReadOnlyDictionary<string, string> settings);

    /// <summary>
    /// Creates or reuses the configuration's connector and probes it; never throws for a failing provider.
    /// </summary>
    Task<AdapterHealth> CheckHealthAsync(IntegrationConfiguration configuration, CancellationToken cancellationToken);
}
