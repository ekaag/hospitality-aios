using HospitalityOS.Adapters.EfCore.Persistence;
using HospitalityOS.Domain.Properties.Policies;
using HospitalityOS.Ports.Tenancy;
using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Adapters.EfCore.Repositories;

internal sealed class EfPolicyRepository(HospitalityDbContext context, EfUnitOfWork unitOfWork, ICurrentTenant currentTenant)
    : EfRepository<Policy, PolicyId>(context, unitOfWork, currentTenant), IPolicyRepository
{
    public Task<IReadOnlyList<Policy>> ListForPropertyAsync(PropertyId propertyId, CancellationToken cancellationToken) =>
        QueryAsync(aggregates => aggregates.Where(policy => policy.PropertyId == propertyId), cancellationToken);

    public Task<IReadOnlyList<Policy>> ListActiveForPropertyAsync(PropertyId propertyId, CancellationToken cancellationToken) =>
        QueryAsync(aggregates => aggregates.Where(policy => policy.PropertyId == propertyId && policy.Status == PolicyStatus.Active), cancellationToken);
}
