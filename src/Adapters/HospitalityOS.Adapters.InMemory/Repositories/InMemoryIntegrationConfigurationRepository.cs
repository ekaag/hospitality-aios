using HospitalityOS.Adapters.InMemory.Persistence;
using HospitalityOS.Domain.Integrations.IntegrationConfigurations;
using HospitalityOS.Ports.Tenancy;
using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Adapters.InMemory.Repositories;

internal sealed class InMemoryIntegrationConfigurationRepository(InMemoryStore store, InMemoryUnitOfWork unitOfWork, ICurrentTenant currentTenant)
    : InMemoryRepository<IntegrationConfiguration, IntegrationConfigurationId>(store, unitOfWork, currentTenant), IIntegrationConfigurationRepository
{
    public Task<IntegrationConfiguration?> FindForPropertyAsync(PropertyId propertyId, ConnectorKind kind, CancellationToken cancellationToken) =>
        FindFirstAsync(configuration => configuration.PropertyId == propertyId && configuration.Kind == kind, cancellationToken);

    public Task<IReadOnlyList<IntegrationConfiguration>> ListForPropertyAsync(PropertyId propertyId, CancellationToken cancellationToken) =>
        QueryAsync(aggregates => aggregates.Where(configuration => configuration.PropertyId == propertyId), cancellationToken);
}
