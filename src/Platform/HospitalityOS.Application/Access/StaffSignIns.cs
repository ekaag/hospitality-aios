using HospitalityOS.Application.Administration;
using HospitalityOS.Contracts.Api.Access;
using HospitalityOS.Domain.Organizations.Access;
using HospitalityOS.Ports.Execution;
using HospitalityOS.Ports.Tenancy;
using HospitalityOS.SharedKernel.Execution;
using HospitalityOS.SharedKernel.Results;
using HospitalityOS.SharedKernel.Tenancy;

namespace HospitalityOS.Application.Access;

internal sealed class StaffSignIns(
    IStaffAccessDirectory directory,
    IStaffAccessRepository access,
    TenantContext tenantContext,
    OperationContext operation,
    AuditedCommit commit,
    RecordIds ids,
    TimeProvider timeProvider) : IStaffSignIns
{
    public async Task<Result<StaffAccessResponse>> RedeemAsync(string? code, string issuer, string subject, CancellationToken cancellationToken)
    {
        if (InviteCodes.Hash(code) is not { } hash
            || await directory.FindByInviteAsync(hash, cancellationToken).ConfigureAwait(false) is not { } entry)
        {
            return StaffAccessErrors.InviteNotFound;
        }

        // A linked sign-in already acts in a tenant, possibly another, so nothing is recorded for it.
        if (tenantContext.IsSet || await directory.FindBySignInAsync(issuer, subject, cancellationToken).ConfigureAwait(false) is not null)
        {
            return StaffAccessErrors.AlreadyLinked;
        }

        // From here the invite names the tenant, so the attempt is recorded there, by whoever it would make them.
        tenantContext.Set(entry.TenantId);
        StaffAccess? found = await access.GetByIdAsync(entry.AccessId, cancellationToken).ConfigureAwait(false);

        if (found is not null)
        {
            operation.ActAs(Actor.Staff(found.StaffMemberId?.ToString() ?? found.Id.ToString(), found.Role));
        }

        return await commit.ChangeAsync(
            AdministrationActions.StaffAccessRedeem,
            StaffAccessAdministration.Target(entry.AccessId.Value),
            found,
            StaffAccessErrors.InviteNotFound,
            null,
            invited => Task.FromResult(invited.Redeem(hash, issuer, subject, timeProvider.GetUtcNow())),
            StaffAccessAdministration.Response,
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<Result<StaffInviteResponse>> GrantFirstAdminAsync(
        TenantId tenantId, GrantFirstAdminRequest request, string operatorId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // An operator with access acts as that access already; first administrators are invited from the platform.
        if (tenantContext.IsSet)
        {
            return StaffAccessErrors.OperatorInTenant;
        }

        tenantContext.Set(tenantId);
        operation.ActAs(Actor.Staff(operatorId, StaffRole.Admin));
        IReadOnlyList<StaffAccess> existing = await access.ListAsync(cancellationToken).ConfigureAwait(false);

        if (existing.Any(entry => entry is { Role: StaffRole.Admin, RevokedAt: null }))
        {
            return await commit.RejectAsync(AdministrationActions.StaffAccessGrantFirstAdmin, StaffAccessErrors.TenantHasAdmin, null, cancellationToken).ConfigureAwait(false);
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        (string code, string hash) = InviteCodes.New();
        StaffInvite invite = new(hash, now + InviteCodes.Lifetime);
        Result<StaffAccess> created = StaffAccess.Create(new StaffAccessId(ids.Next()), tenantId, request.DisplayName!, StaffRole.Admin, null, invite, now);

        return await commit.CreateAsync(
            AdministrationActions.StaffAccessGrantFirstAdmin,
            created,
            granted => access.AddAsync(granted, cancellationToken),
            granted => StaffAccessAdministration.Target(granted.Id.Value),
            granted => StaffAccessAdministration.Invite(granted, code, invite),
            cancellationToken).ConfigureAwait(false);
    }
}
