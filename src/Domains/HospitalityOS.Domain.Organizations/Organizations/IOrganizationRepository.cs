using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Domain.Organizations.Organizations;

/// <summary>
/// Persistence port for <see cref="Organization"/>. Implementations are scoped to the current tenant
/// (P10) and commit through the unit of work.
/// </summary>
public interface IOrganizationRepository
{
    /// <summary>
    /// Loads an organization, or returns <c>null</c> when it does not exist in the current tenant.
    /// </summary>
    Task<Organization?> GetByIdAsync(OrganizationId id, CancellationToken cancellationToken);

    /// <summary>
    /// The current tenant's organizations, in no particular order.
    /// </summary>
    Task<IReadOnlyList<Organization>> ListAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Adds a new organization, persisted when the unit of work commits.
    /// </summary>
    Task AddAsync(Organization organization, CancellationToken cancellationToken);
}
