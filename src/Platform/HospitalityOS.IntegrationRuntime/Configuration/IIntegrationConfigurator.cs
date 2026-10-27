using HospitalityOS.Domain.Integrations.IntegrationConfigurations;
using HospitalityOS.SharedKernel.Ids;
using HospitalityOS.SharedKernel.Results;

namespace HospitalityOS.IntegrationRuntime.Configuration;

/// <summary>
/// Chooses which registered provider serves each of a property's connectors (spec §6 <c>IntegrationConfiguration</c>).
/// Changes are staged; the caller commits them through the unit of work, which records their events. Scoped.
/// </summary>
public interface IIntegrationConfigurator
{
    /// <summary>
    /// The registered providers of one connector kind, ordered, e.g. for an administrator to choose from.
    /// </summary>
    IReadOnlyList<string> ListProviders(ConnectorKind kind);

    /// <summary>
    /// Sets the provider and settings for one of the property's connectors, creating its configuration or replacing the
    /// existing one (a provider swap, Journey 18). The provider name matches ignoring case and is stored as registered;
    /// the provider checks the settings. A disabled configuration stays disabled.
    /// </summary>
    /// <param name="propertyId">The property, in the current tenant.</param>
    /// <param name="kind">Which connector.</param>
    /// <param name="providerName">A registered provider of that kind.</param>
    /// <param name="settings">The provider's non-secret settings.</param>
    /// <param name="cancellationToken">Cancels loading.</param>
    /// <returns>The configuration's ID; <c>property.not_found</c>; or a <see cref="ValidationError"/>
    /// (<see cref="IntegrationRuntimeErrors.ProviderUnknownCode"/>, <see cref="IntegrationRuntimeErrors.SettingsInvalidCode"/>,
    /// or a setting rule).</returns>
    Task<Result<IntegrationConfigurationId>> ConfigureAsync(
        PropertyId propertyId,
        ConnectorKind kind,
        string providerName,
        IReadOnlyDictionary<string, string> settings,
        CancellationToken cancellationToken);

    /// <summary>
    /// Switches one of the property's connectors on or off; switching to the current state is a no-op.
    /// </summary>
    /// <returns>Success, or <see cref="IntegrationRuntimeErrors.NotConfigured"/>.</returns>
    Task<Result> SetEnabledAsync(PropertyId propertyId, ConnectorKind kind, bool enabled, CancellationToken cancellationToken);
}
