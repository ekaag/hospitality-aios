using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Domain.Guests.Guests;

/// <summary>
/// Persistence port for <see cref="Guest"/>. Implementations are scoped to the current tenant (P10) and commit
/// through the unit of work.
/// </summary>
public interface IGuestRepository
{
    /// <summary>
    /// Loads a guest, or returns <c>null</c> when it does not exist in the current tenant.
    /// </summary>
    Task<Guest?> GetByIdAsync(GuestId id, CancellationToken cancellationToken);

    /// <summary>
    /// Adds a new guest, persisted when the unit of work commits.
    /// </summary>
    Task AddAsync(Guest guest, CancellationToken cancellationToken);
}
