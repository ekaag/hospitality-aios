using HospitalityOS.Contracts.Api.Access;
using HospitalityOS.SharedKernel.Results;
using HospitalityOS.SharedKernel.Tenancy;

namespace HospitalityOS.Application.Access;

/// <summary>
/// What a signed-in person does before they act in a tenant (K4b): redeem an invite, which tells the scope its tenant,
/// or, as a platform operator, invite a customer's first administrator. Scoped; it sets the scope's tenant and actor, so
/// the scope must not have acted for another tenant.
/// </summary>
public interface IStaffSignIns
{
    /// <summary>
    /// Links the sign-in to the access the code opens.
    /// </summary>
    /// <param name="code">The invite code.</param>
    /// <param name="issuer">The caller's token issuer.</param>
    /// <param name="subject">The caller's stable user ID at the issuer.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The access; <c>access.invite_not_found</c> (not audited: no tenant to record it in);
    /// <c>access.already_linked</c> when the sign-in already acts as someone; <c>access.invite_expired</c>;
    /// <c>access.revoked</c>; or a conflict when the sign-in was linked elsewhere at the same moment.</returns>
    Task<Result<StaffAccessResponse>> RedeemAsync(string? code, string issuer, string subject, CancellationToken cancellationToken);

    /// <summary>
    /// Invites a tenant's first administrator, as an operator.
    /// </summary>
    /// <param name="tenantId">The customer's tenant.</param>
    /// <param name="request">Who they are.</param>
    /// <param name="operatorId">The operator, as audit records name them.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The invite; <c>access.tenant_has_admin</c> when an administrator's access is already active or invited,
    /// so operators cannot add themselves to customers' tenants; or a <see cref="ValidationError"/>.</returns>
    Task<Result<StaffInviteResponse>> GrantFirstAdminAsync(TenantId tenantId, GrantFirstAdminRequest request, string operatorId, CancellationToken cancellationToken);
}
