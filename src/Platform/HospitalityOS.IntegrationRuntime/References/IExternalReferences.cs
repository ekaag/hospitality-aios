using HospitalityOS.Domain.Integrations.ExternalReferences;
using HospitalityOS.IntegrationContracts;
using HospitalityOS.SharedKernel.Ids;
using HospitalityOS.SharedKernel.Results;

namespace HospitalityOS.IntegrationRuntime.References;

/// <summary>
/// Maps providers' IDs to canonical records (spec §6 <c>ExternalReference</c>, P3): code that reads a connector looks up
/// or links the provider's ID here, so canonical IDs survive a provider swap (Journey 18). Links are staged; the caller
/// commits them through the unit of work. Scoped; current tenant only.
/// </summary>
public interface IExternalReferences
{
    /// <summary>
    /// The canonical ID linked to a provider's ID, or <c>null</c> when it is not linked.
    /// </summary>
    /// <param name="propertyId">The property whose provider issued the ID.</param>
    /// <param name="providerName">The provider, as <c>ResolvedConnector.ProviderName</c> gives it.</param>
    /// <param name="entityType">The canonical type name, e.g. <c>nameof(StayContext)</c>.</param>
    /// <param name="externalId">The provider's ID, matched exactly.</param>
    /// <param name="cancellationToken">Cancels the lookup.</param>
    Task<Guid?> FindCanonicalIdAsync(PropertyId propertyId, string providerName, string entityType, ExternalId externalId, CancellationToken cancellationToken);

    /// <summary>
    /// Every provider's IDs for one canonical record, in no particular order.
    /// </summary>
    Task<IReadOnlyList<ExternalReference>> ListForCanonicalAsync(PropertyId propertyId, string entityType, Guid canonicalId, CancellationToken cancellationToken);

    /// <summary>
    /// Links a provider's ID to a canonical record. Linking it again to the same record returns the existing reference
    /// (P8). A race between two scopes linking the same ID fails one commit as a duplicate key.
    /// </summary>
    /// <returns>The reference; <c>property.not_found</c>; <see cref="IntegrationRuntimeErrors.ReferenceConflict"/> when the
    /// ID is linked to another record; or a <see cref="ValidationError"/>.</returns>
    Task<Result<ExternalReference>> LinkAsync(
        PropertyId propertyId,
        string providerName,
        string entityType,
        ExternalId externalId,
        Guid canonicalId,
        CancellationToken cancellationToken);
}
