using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Domain.Guests.StayContexts;

/// <summary>
/// Persistence port for <see cref="StayContext"/>. Implementations are scoped to the current tenant (P10) and
/// commit through the unit of work.
/// </summary>
public interface IStayContextRepository
{
    /// <summary>
    /// Loads a stay, or returns <c>null</c> when it does not exist in the current tenant.
    /// </summary>
    Task<StayContext?> GetByIdAsync(StayContextId id, CancellationToken cancellationToken);

    /// <summary>
    /// Finds the guest's in-house stay at a property, or returns <c>null</c> (Journey 12: identify the active stay).
    /// </summary>
    Task<StayContext?> FindInHouseAsync(PropertyId propertyId, GuestId guestId, CancellationToken cancellationToken);

    /// <summary>
    /// Finds the in-house stay in a unit, or returns <c>null</c> when nobody is staying there (matching a PMS stay to an
    /// existing one when its provider is swapped, Journey 18). Should several be in house, any one of them.
    /// </summary>
    Task<StayContext?> FindInHouseInUnitAsync(PropertyId propertyId, AccommodationUnitId unitId, CancellationToken cancellationToken);

    /// <summary>
    /// Adds a new stay, persisted when the unit of work commits.
    /// </summary>
    Task AddAsync(StayContext stayContext, CancellationToken cancellationToken);
}
