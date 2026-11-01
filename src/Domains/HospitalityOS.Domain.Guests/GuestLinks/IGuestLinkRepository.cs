using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Domain.Guests.GuestLinks;

/// <summary>
/// Persistence port for <see cref="GuestLink"/>. Implementations are scoped to the current tenant (P10) and commit
/// through the unit of work.
/// </summary>
public interface IGuestLinkRepository
{
    /// <summary>
    /// Loads a link, or returns <c>null</c> when it does not exist in the current tenant.
    /// </summary>
    Task<GuestLink?> GetByIdAsync(GuestLinkId id, CancellationToken cancellationToken);

    /// <summary>
    /// The stay's open link, or <c>null</c>.
    /// </summary>
    Task<GuestLink?> FindOpenForStayAsync(StayContextId stayContextId, CancellationToken cancellationToken);

    /// <summary>
    /// The property's open links, in no particular order.
    /// </summary>
    Task<IReadOnlyList<GuestLink>> ListOpenForPropertyAsync(PropertyId propertyId, CancellationToken cancellationToken);

    /// <summary>
    /// Adds a new link, persisted when the unit of work commits.
    /// </summary>
    Task AddAsync(GuestLink link, CancellationToken cancellationToken);
}
