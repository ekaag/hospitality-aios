using HospitalityOS.Adapters.EfCore.Persistence;
using HospitalityOS.Domain.Integrations.ExternalReferences;
using HospitalityOS.Ports.Tenancy;
using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Adapters.EfCore.Repositories;

internal sealed class EfExternalReferenceRepository(HospitalityDbContext context, EfUnitOfWork unitOfWork, ICurrentTenant currentTenant)
    : EfRepository<ExternalReference, ExternalReferenceId>(context, unitOfWork, currentTenant), IExternalReferenceRepository
{
    public Task<ExternalReference?> FindAsync(PropertyId propertyId, string providerName, string entityType, string externalId, CancellationToken cancellationToken) =>
        FindFirstAsync(
            reference => reference.PropertyId == propertyId
                && reference.ProviderName == providerName
                && reference.EntityType == entityType
                && reference.ExternalId == externalId,
            cancellationToken);

    public Task<IReadOnlyList<ExternalReference>> ListForCanonicalAsync(PropertyId propertyId, string entityType, Guid canonicalId, CancellationToken cancellationToken) =>
        QueryAsync(
            aggregates => aggregates.Where(reference => reference.PropertyId == propertyId && reference.EntityType == entityType && reference.CanonicalId == canonicalId),
            cancellationToken);
}
