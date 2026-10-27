using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.IntegrationRuntime.Health;

/// <summary>
/// Checks a property's configured connectors. Scoped; reads the current tenant's configuration.
/// </summary>
public interface IIntegrationHealth
{
    /// <summary>
    /// One report per configured connector, ordered by kind. Active connectors are created if needed and probed when
    /// they implement <c>IAdapterHealthProbe</c>; disabled ones are not checked; failures are reported, never thrown.
    /// </summary>
    /// <param name="propertyId">The property.</param>
    /// <param name="cancellationToken">Cancels the checks.</param>
    /// <returns>The reports; empty when nothing is configured or the property is not in the tenant.</returns>
    Task<IReadOnlyList<IntegrationHealthReport>> CheckAsync(PropertyId propertyId, CancellationToken cancellationToken);
}
