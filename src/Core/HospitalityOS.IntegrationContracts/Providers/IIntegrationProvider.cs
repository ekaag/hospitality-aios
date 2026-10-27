namespace HospitalityOS.IntegrationContracts.Providers;

/// <summary>
/// One provider of a connector (spec §6 <c>IntegrationProvider</c>), e.g. <c>MockPms</c> for <c>IPmsConnector</c>.
/// Integration projects register each provider as an <c>IIntegrationProvider&lt;TConnector&gt;</c> singleton; a property's
/// integration configuration then selects one by <see cref="Name"/>, so swapping providers is a configuration change
/// (Journey 18).
/// </summary>
/// <typeparam name="TConnector">The connector interface the provider implements, e.g. <c>IPmsConnector</c>.</typeparam>
public interface IIntegrationProvider<out TConnector>
    where TConnector : class
{
    /// <summary>
    /// The name configurations use to select this provider: a letter, then letters and digits, e.g. <c>MockPms</c>.
    /// Unique per connector, ignoring case.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Checks a property's settings when the property is configured, so mistakes are reported to the administrator
    /// rather than at first use.
    /// </summary>
    /// <param name="settings">The settings, by name, ignoring case.</param>
    /// <returns>Human-readable problems, free of setting values; empty when the settings are valid.</returns>
    IEnumerable<string> ValidateSettings(IReadOnlyDictionary<string, string> settings) => [];

    /// <summary>
    /// Creates the connector serving one property. Called once per configuration version; the instance is cached and
    /// shared, so it must be thread-safe. It may also implement <c>IAdapterHealthProbe</c> for health checks.
    /// </summary>
    /// <param name="context">The property served and its settings.</param>
    /// <returns>The connector.</returns>
    TConnector Create(ConnectorContext context);
}
