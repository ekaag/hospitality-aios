namespace HospitalityOS.IntegrationRuntime.Resolution;

/// <summary>
/// A property's connector and the provider behind it.
/// </summary>
/// <param name="Connector">The connector.</param>
/// <param name="ProviderName">The provider's registered name; external IDs from the connector are linked under it.</param>
/// <typeparam name="TConnector">The connector interface.</typeparam>
public sealed record ResolvedConnector<TConnector>(TConnector Connector, string ProviderName)
    where TConnector : class;
