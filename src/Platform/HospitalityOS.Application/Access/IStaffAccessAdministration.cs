using HospitalityOS.Contracts.Api.Access;
using HospitalityOS.SharedKernel.Results;

namespace HospitalityOS.Application.Access;

/// <summary>
/// Who has access to the caller's tenant (K4b): administrators invite people with one-time codes, send new codes, and
/// revoke access. Commands commit their change and its audit record together; rejected commands are audited too.
/// Scoped; the caller's tenant and actor come from the operation.
/// </summary>
public interface IStaffAccessAdministration
{
    /// <summary>
    /// Gives someone access and invites them; the code is in the response only.
    /// </summary>
    /// <returns>The invite; <c>access.staff_member_taken</c> when the staff member already has access; or a
    /// <see cref="ValidationError"/> (a staff member not in the tenant is the field <c>staffMemberId</c>).</returns>
    Task<Result<StaffInviteResponse>> GrantAsync(GrantStaffAccessRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Replaces any open invite with a new code and unlinks the current sign-in.
    /// </summary>
    /// <returns>The invite; <c>access.not_found</c>; or <c>access.revoked</c>.</returns>
    Task<Result<StaffInviteResponse>> ReinviteAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Ends someone's access; their next request is refused. Revoking it again is a no-op.
    /// </summary>
    /// <returns>The access; <c>access.not_found</c>; or <c>access.revoke_self</c>, since an administrator revoking
    /// themselves could leave nobody to invite anyone.</returns>
    Task<Result<StaffAccessResponse>> RevokeAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Reads someone's access.
    /// </summary>
    /// <returns>The access, or <c>access.not_found</c>.</returns>
    Task<Result<StaffAccessResponse>> GetAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Everyone with access, revoked or not, by name.
    /// </summary>
    Task<IReadOnlyList<StaffAccessResponse>> ListAsync(CancellationToken cancellationToken);
}
