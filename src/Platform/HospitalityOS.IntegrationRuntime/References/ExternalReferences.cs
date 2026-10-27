using HospitalityOS.Domain.Integrations.ExternalReferences;
using HospitalityOS.Domain.Properties.Properties;
using HospitalityOS.IntegrationContracts;
using HospitalityOS.Ports.Bindings;
using HospitalityOS.Ports.Ids;
using HospitalityOS.SharedKernel.Ids;
using HospitalityOS.SharedKernel.Results;

namespace HospitalityOS.IntegrationRuntime.References;

internal sealed class ExternalReferences(
    IExternalReferenceRepository references,
    IPropertyRepository properties,
    IBindingResolver<IIdGenerator> idGenerators,
    TimeProvider timeProvider) : IExternalReferences
{
    public async Task<Guid?> FindCanonicalIdAsync(PropertyId propertyId, string providerName, string entityType, ExternalId externalId, CancellationToken cancellationToken)
    {
        ExternalReference? reference = await references.FindAsync(propertyId, providerName, entityType, externalId.Value, cancellationToken).ConfigureAwait(false);

        return reference?.CanonicalId;
    }

    public Task<IReadOnlyList<ExternalReference>> ListForCanonicalAsync(PropertyId propertyId, string entityType, Guid canonicalId, CancellationToken cancellationToken) =>
        references.ListForCanonicalAsync(propertyId, entityType, canonicalId, cancellationToken);

    public async Task<Result<ExternalReference>> LinkAsync(
        PropertyId propertyId,
        string providerName,
        string entityType,
        ExternalId externalId,
        Guid canonicalId,
        CancellationToken cancellationToken)
    {
        ExternalReference? existing = await references.FindAsync(propertyId, providerName, entityType, externalId.Value, cancellationToken).ConfigureAwait(false);

        if (existing is not null)
        {
            return existing.CanonicalId == canonicalId
                ? existing
                : IntegrationRuntimeErrors.ReferenceConflict(providerName, entityType, externalId.Value);
        }

        Property? property = await properties.GetByIdAsync(propertyId, cancellationToken).ConfigureAwait(false);

        if (property is null)
        {
            return IntegrationRuntimeErrors.PropertyNotFound;
        }

        Result<ExternalReference> created = ExternalReference.Create(
            new ExternalReferenceId(idGenerators.Resolve().NewId()),
            property.TenantId,
            propertyId,
            providerName,
            entityType,
            externalId.Value,
            canonicalId,
            timeProvider.GetUtcNow());

        if (created.IsSuccess)
        {
            await references.AddAsync(created.Value, cancellationToken).ConfigureAwait(false);
        }

        return created;
    }
}
