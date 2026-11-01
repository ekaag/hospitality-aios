using HospitalityOS.SharedKernel.Execution;
using HospitalityOS.SharedKernel.Ids;
using HospitalityOS.SharedKernel.Tenancy;

namespace HospitalityOS.Domain.Organizations.Access;

/// <summary>
/// Finds access across every tenant, by what a caller brings before their tenant is known: their sign-in, or an invite
/// code's hash. The only reads not filtered to the current tenant (P10), so it answers with no more than who the caller
/// is and in which tenant; changes load the access through <see cref="IStaffAccessRepository"/> in that tenant.
/// </summary>
public interface IStaffAccessDirectory
{
    /// <summary>
    /// The active access linked to a sign-in, or <c>null</c> when none is (never linked, or revoked).
    /// </summary>
    Task<StaffSignIn?> FindBySignInAsync(string issuer, string subject, CancellationToken cancellationToken);

    /// <summary>
    /// The access an open invite belongs to, or <c>null</c> when no open invite has the hash.
    /// </summary>
    Task<StaffAccessEntry?> FindByInviteAsync(string inviteCodeHash, CancellationToken cancellationToken);
}

/// <summary>
/// Whose access an invite opens, and in which tenant.
/// </summary>
public sealed record StaffAccessEntry(TenantId TenantId, StaffAccessId AccessId);

/// <summary>
/// Who a linked sign-in is: their tenant, their access, their role, and for workers their roster entry.
/// </summary>
public sealed record StaffSignIn(TenantId TenantId, StaffAccessId AccessId, StaffRole Role, StaffMemberId? StaffMemberId)
{
    /// <summary>
    /// The ID they act as in events and audit records: their staff member ID when on a roster, so the work routed to them
    /// is theirs; otherwise their access ID.
    /// </summary>
    public string ActorId => StaffMemberId?.ToString() ?? AccessId.ToString();
}
