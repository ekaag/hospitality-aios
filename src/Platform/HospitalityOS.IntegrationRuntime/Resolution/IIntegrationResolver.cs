using HospitalityOS.SharedKernel.Ids;
using HospitalityOS.SharedKernel.Results;

namespace HospitalityOS.IntegrationRuntime.Resolution;

/// <summary>
/// Hands out the connector that serves a property, as its integration configuration selects (spec §10, Journey 18).
/// Business code depends on this and the connector interface, never on a provider. Scoped; reads the current tenant's
/// configuration.
/// </summary>
/// <typeparam name="TConnector">A connector interface from <c>HospitalityOS.IntegrationContracts</c>, e.g.
/// <c>IMessagingConnector</c>.</typeparam>
public interface IIntegrationResolver<TConnector>
    where TConnector : class
{
    /// <summary>
    /// Resolves the property's connector. The same instance is returned until the configuration changes.
    /// </summary>
    /// <param name="propertyId">The property served.</param>
    /// <param name="cancellationToken">Cancels loading the configuration.</param>
    /// <returns>The connector with its provider's name (for <c>ExternalReference</c>s), or
    /// <see cref="IntegrationRuntimeErrors.NotConfigured"/>, <see cref="IntegrationRuntimeErrors.Disabled"/>, or
    /// <see cref="IntegrationRuntimeErrors.ProviderUnavailable"/>.</returns>
    Task<Result<ResolvedConnector<TConnector>>> ResolveAsync(PropertyId propertyId, CancellationToken cancellationToken);
}
