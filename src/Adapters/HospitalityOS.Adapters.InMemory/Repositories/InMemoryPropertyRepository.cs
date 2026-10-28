using HospitalityOS.Adapters.InMemory.Persistence;
using HospitalityOS.Domain.Properties.Properties;
using HospitalityOS.Ports.Tenancy;
using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Adapters.InMemory.Repositories;

internal sealed class InMemoryPropertyRepository(InMemoryStore store, InMemoryUnitOfWork unitOfWork, ICurrentTenant currentTenant)
    : InMemoryRepository<Property, PropertyId>(store, unitOfWork, currentTenant), IPropertyRepository
{
    public Task<IReadOnlyList<Property>> ListAsync(OrganizationId? organizationId, CancellationToken cancellationToken) =>
        QueryAsync(
            aggregates => organizationId is { } organization ? aggregates.Where(property => property.OrganizationId == organization) : aggregates,
            cancellationToken);
}
