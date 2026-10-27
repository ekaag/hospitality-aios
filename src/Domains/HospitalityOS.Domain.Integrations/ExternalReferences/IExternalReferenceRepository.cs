using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Domain.Integrations.ExternalReferences;

/// <summary>
/// Persistence port for <see cref="ExternalReference"/>. Implementations are scoped to the current tenant (P10) and
/// commit through the unit of work.
/// </summary>
public interface IExternalReferenceRepository
{
    /// <summary>
    /// Loads a reference, or returns <c>null</c> when it does not exist in the current tenant.
    /// </summary>
    Task<ExternalReference?> GetByIdAsync(ExternalReferenceId id, CancellationToken cancellationToken);

    /// <summary>
    /// The reference for a provider's ID, matched exactly, or <c>null</c> when the ID is not linked.
    /// </summary>
    Task<ExternalReference?> FindAsync(PropertyId propertyId, string providerName, string entityType, string externalId, CancellationToken cancellationToken);

    /// <summary>
    /// Every provider's references to one canonical record, in no particular order.
    /// </summary>
    Task<IReadOnlyList<ExternalReference>> ListForCanonicalAsync(PropertyId propertyId, string entityType, Guid canonicalId, CancellationToken cancellationToken);

    /// <summary>
    /// Adds a new reference, persisted when the unit of work commits. A provider ID already linked fails the commit as a
    /// duplicate key.
    /// </summary>
    Task AddAsync(ExternalReference reference, CancellationToken cancellationToken);
}
