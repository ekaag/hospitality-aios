using HospitalityOS.Adapters.InMemory.Persistence;
using HospitalityOS.Domain.Organizations.Access;

namespace HospitalityOS.Adapters.InMemory.Repositories;

/// <summary>
/// The one read past the tenant filter (see <see cref="IStaffAccessDirectory"/>), over committed access only.
/// </summary>
internal sealed class InMemoryStaffAccessDirectory(InMemoryStore store) : IStaffAccessDirectory
{
    public Task<StaffSignIn?> FindBySignInAsync(string issuer, string subject, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        StaffAccess? access = AllTenants().FirstOrDefault(access =>
            access.SignInIssuer == issuer && access.SignInSubject == subject && access.RevokedAt is null);

        return Task.FromResult(access is null ? null : new StaffSignIn(access.TenantId, access.Id, access.Role, access.StaffMemberId));
    }

    public Task<StaffAccessEntry?> FindByInviteAsync(string inviteCodeHash, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        StaffAccess? access = AllTenants().FirstOrDefault(access => access.InviteCodeHash == inviteCodeHash);

        return Task.FromResult(access is null ? null : new StaffAccessEntry(access.TenantId, access.Id));
    }

    private IEnumerable<StaffAccess> AllTenants() => store.ListAllTenants(typeof(StaffAccess)).Select(stored => (StaffAccess)stored.Snapshot);
}
