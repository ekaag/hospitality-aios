using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Domain.Integrations.IntegrationConfigurations;

/// <summary>
/// Persistence port for <see cref="IntegrationConfiguration"/>. Implementations are scoped to the current tenant (P10)
/// and commit through the unit of work.
/// </summary>
public interface IIntegrationConfigurationRepository
{
    /// <summary>
    /// Loads a configuration, or returns <c>null</c> when it does not exist in the current tenant.
    /// </summary>
    Task<IntegrationConfiguration?> GetByIdAsync(IntegrationConfigurationId id, CancellationToken cancellationToken);

    /// <summary>
    /// The property's configuration for one kind of connector, or <c>null</c> when there is none.
    /// </summary>
    Task<IntegrationConfiguration?> FindForPropertyAsync(PropertyId propertyId, ConnectorKind kind, CancellationToken cancellationToken);

    /// <summary>
    /// The property's configurations, in no particular order.
    /// </summary>
    Task<IReadOnlyList<IntegrationConfiguration>> ListForPropertyAsync(PropertyId propertyId, CancellationToken cancellationToken);

    /// <summary>
    /// Adds a new configuration, persisted when the unit of work commits. A second configuration of the same kind for
    /// the property fails the commit as a duplicate key.
    /// </summary>
    Task AddAsync(IntegrationConfiguration configuration, CancellationToken cancellationToken);
}
