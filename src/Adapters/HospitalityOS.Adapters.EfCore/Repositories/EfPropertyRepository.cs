using HospitalityOS.Adapters.EfCore.Persistence;
using HospitalityOS.Domain.Properties.Properties;
using HospitalityOS.Ports.Tenancy;
using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Adapters.EfCore.Repositories;

internal sealed class EfPropertyRepository(HospitalityDbContext context, EfUnitOfWork unitOfWork, ICurrentTenant currentTenant)
    : EfRepository<Property, PropertyId>(context, unitOfWork, currentTenant), IPropertyRepository
{
    public Task<IReadOnlyList<Property>> ListAsync(OrganizationId? organizationId, CancellationToken cancellationToken) =>
        QueryAsync(
            aggregates => organizationId is { } organization ? aggregates.Where(property => property.OrganizationId == organization) : aggregates,
            cancellationToken);
}
