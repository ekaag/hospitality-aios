using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Domain.Organizations.Access;

/// <summary>
/// Persistence port for <see cref="StaffAccess"/>. Implementations are scoped to the current tenant (P10) and commit
/// through the unit of work.
/// </summary>
public interface IStaffAccessRepository
{
    /// <summary>
    /// Loads someone's access, or returns <c>null</c> when it does not exist in the current tenant.
    /// </summary>
    Task<StaffAccess?> GetByIdAsync(StaffAccessId id, CancellationToken cancellationToken);

    /// <summary>
    /// The access given for a staff member, revoked or not, or <c>null</c>.
    /// </summary>
    Task<StaffAccess?> FindByStaffMemberAsync(StaffMemberId staffMemberId, CancellationToken cancellationToken);

    /// <summary>
    /// The current tenant's access, in no particular order.
    /// </summary>
    Task<IReadOnlyList<StaffAccess>> ListAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Adds new access, persisted when the unit of work commits.
    /// </summary>
    Task AddAsync(StaffAccess access, CancellationToken cancellationToken);
}
