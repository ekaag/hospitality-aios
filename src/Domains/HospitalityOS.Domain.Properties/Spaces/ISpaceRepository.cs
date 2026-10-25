using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Domain.Properties.Spaces;

/// <summary>
/// Persistence port for <see cref="Space"/>. Implementations are scoped to the current tenant (P10) and
/// commit through the unit of work.
/// </summary>
public interface ISpaceRepository
{
    /// <summary>
    /// Loads a space, or returns <c>null</c> when it does not exist in the current tenant.
    /// </summary>
    Task<Space?> GetByIdAsync(SpaceId id, CancellationToken cancellationToken);

    /// <summary>
    /// Adds a new space, persisted when the unit of work commits.
    /// </summary>
    Task AddAsync(Space space, CancellationToken cancellationToken);
}
