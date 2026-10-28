using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Domain.Properties.Properties;

/// <summary>
/// Persistence port for <see cref="Property"/>. Implementations are scoped to the current tenant
/// (P10) and commit through the unit of work.
/// </summary>
public interface IPropertyRepository
{
    /// <summary>
    /// Loads a property, or returns <c>null</c> when it does not exist in the current tenant.
    /// </summary>
    Task<Property?> GetByIdAsync(PropertyId id, CancellationToken cancellationToken);

    /// <summary>
    /// The current tenant's properties, or one organization's, in no particular order.
    /// </summary>
    Task<IReadOnlyList<Property>> ListAsync(OrganizationId? organizationId, CancellationToken cancellationToken);

    /// <summary>
    /// Adds a new property, persisted when the unit of work commits.
    /// </summary>
    Task AddAsync(Property property, CancellationToken cancellationToken);
}
