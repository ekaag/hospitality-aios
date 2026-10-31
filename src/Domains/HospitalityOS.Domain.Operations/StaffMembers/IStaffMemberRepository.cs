using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Domain.Operations.StaffMembers;

/// <summary>
/// Persistence port for <see cref="StaffMember"/>. Implementations are scoped to the current tenant (P10) and commit
/// through the unit of work.
/// </summary>
public interface IStaffMemberRepository
{
    /// <summary>
    /// Loads a staff member, or returns <c>null</c> when they do not exist in the current tenant.
    /// </summary>
    Task<StaffMember?> GetByIdAsync(StaffMemberId id, CancellationToken cancellationToken);

    /// <summary>
    /// A property's roster, on duty or not, in no particular order.
    /// </summary>
    Task<IReadOnlyList<StaffMember>> ListForPropertyAsync(PropertyId propertyId, CancellationToken cancellationToken);

    /// <summary>
    /// Adds a new staff member, persisted when the unit of work commits.
    /// </summary>
    Task AddAsync(StaffMember staffMember, CancellationToken cancellationToken);
}
