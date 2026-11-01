using HospitalityOS.Adapters.EfCore.Persistence;
using HospitalityOS.Domain.Organizations.Access;
using Microsoft.EntityFrameworkCore;

namespace HospitalityOS.Adapters.EfCore.Repositories;

/// <summary>
/// The one query past the tenant filter (see <see cref="IStaffAccessDirectory"/>): untracked, by unique keys, and
/// answering with no more than who and in which tenant.
/// </summary>
internal sealed class EfStaffAccessDirectory(HospitalityDbContext context) : IStaffAccessDirectory
{
    private IQueryable<StaffAccess> AllTenants => context.Set<StaffAccess>().IgnoreQueryFilters().AsNoTracking();

    public async Task<StaffSignIn?> FindBySignInAsync(string issuer, string subject, CancellationToken cancellationToken)
    {
        StaffAccess? access = await AllTenants
            .Where(access => access.SignInIssuer == issuer && access.SignInSubject == subject && access.RevokedAt == null)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return access is null ? null : new StaffSignIn(access.TenantId, access.Id, access.Role, access.StaffMemberId);
    }

    public async Task<StaffAccessEntry?> FindByInviteAsync(string inviteCodeHash, CancellationToken cancellationToken)
    {
        StaffAccess? access = await AllTenants
            .Where(access => access.InviteCodeHash == inviteCodeHash)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return access is null ? null : new StaffAccessEntry(access.TenantId, access.Id);
    }
}
