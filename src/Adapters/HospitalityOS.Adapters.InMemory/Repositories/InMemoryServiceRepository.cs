using HospitalityOS.Adapters.InMemory.Persistence;
using HospitalityOS.Domain.Services.Services;
using HospitalityOS.Ports.Tenancy;
using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Adapters.InMemory.Repositories;

internal sealed class InMemoryServiceRepository(InMemoryStore store, InMemoryUnitOfWork unitOfWork, ICurrentTenant currentTenant)
    : InMemoryRepository<Service, ServiceId>(store, unitOfWork, currentTenant), IServiceRepository
{
    public Task<Service?> FindByCodeAsync(PropertyId propertyId, string code, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(code);

        string trimmed = code.Trim();

        return FindFirstAsync(
            service => service.PropertyId == propertyId && string.Equals(service.Details.Code, trimmed, StringComparison.OrdinalIgnoreCase),
            cancellationToken);
    }

    public Task<IReadOnlyList<Service>> ListForPropertyAsync(PropertyId propertyId, CancellationToken cancellationToken) =>
        QueryAsync(aggregates => aggregates.Where(service => service.PropertyId == propertyId), cancellationToken);
}
