using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Domain.Properties.Policies;

/// <summary>
/// Persistence port for <see cref="Policy"/>. Implementations are scoped to the current tenant (P10) and
/// commit through the unit of work. Policy search for answers goes through read-side queries (D3).
/// </summary>
public interface IPolicyRepository
{
    /// <summary>
    /// Loads a policy, or returns <c>null</c> when it does not exist in the current tenant.
    /// </summary>
    Task<Policy?> GetByIdAsync(PolicyId id, CancellationToken cancellationToken);

    /// <summary>
    /// The property's active policies (whatever their effective dates), in no particular order.
    /// </summary>
    Task<IReadOnlyList<Policy>> ListActiveForPropertyAsync(PropertyId propertyId, CancellationToken cancellationToken);

    /// <summary>
    /// The property's policies, whatever their status, in no particular order.
    /// </summary>
    Task<IReadOnlyList<Policy>> ListForPropertyAsync(PropertyId propertyId, CancellationToken cancellationToken);

    /// <summary>
    /// Adds a new policy, persisted when the unit of work commits.
    /// </summary>
    Task AddAsync(Policy policy, CancellationToken cancellationToken);
}
