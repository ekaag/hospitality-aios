using HospitalityOS.Application.Administration;
using HospitalityOS.Application.Auditing;
using HospitalityOS.Contracts.Api.Access;
using HospitalityOS.Domain.Operations.StaffMembers;
using HospitalityOS.Domain.Organizations.Access;
using HospitalityOS.Ports.Execution;
using HospitalityOS.Ports.Tenancy;
using HospitalityOS.SharedKernel.Execution;
using HospitalityOS.SharedKernel.Results;
using ContractStatus = HospitalityOS.Contracts.Api.Access.StaffAccessStatus;

namespace HospitalityOS.Application.Access;

internal sealed class StaffAccessAdministration(
    IStaffAccessRepository access,
    IStaffMemberRepository staff,
    ICurrentTenant currentTenant,
    OperationContext operation,
    AuditedCommit commit,
    RecordIds ids,
    TimeProvider timeProvider) : IStaffAccessAdministration
{
    public async Task<Result<StaffInviteResponse>> GrantAsync(GrantStaffAccessRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        RequestFields fields = new();

        if (request.Role is null)
        {
            fields.Add("role", "access.role_required", "role is required.");
        }

        if (request.StaffMemberId is { } staffMemberId)
        {
            if (await staff.GetByIdAsync(staffMemberId, cancellationToken).ConfigureAwait(false) is null)
            {
                fields.Add("staffMemberId", "access.staff_member_not_found", "There is no such staff member.");
            }
            else if (await access.FindByStaffMemberAsync(staffMemberId, cancellationToken).ConfigureAwait(false) is not null)
            {
                return await commit.RejectAsync(AdministrationActions.StaffAccessGrant, StaffAccessErrors.StaffMemberTaken, null, cancellationToken).ConfigureAwait(false);
            }
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        (string code, string hash) = InviteCodes.New();
        StaffInvite invite = new(hash, now + InviteCodes.Lifetime);
        Result<StaffAccess> created = fields.Merge(StaffAccess.Create(
            new StaffAccessId(ids.Next()), currentTenant.TenantId, request.DisplayName!, request.Role ?? StaffRole.Manager, request.StaffMemberId, invite, now));

        return await commit.CreateAsync(
            AdministrationActions.StaffAccessGrant,
            created,
            granted => access.AddAsync(granted, cancellationToken),
            granted => Target(granted.Id.Value),
            granted => Invite(granted, code, invite),
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<Result<StaffInviteResponse>> ReinviteAsync(Guid id, CancellationToken cancellationToken)
    {
        StaffAccess? found = await FindAsync(id, cancellationToken).ConfigureAwait(false);
        DateTimeOffset now = timeProvider.GetUtcNow();
        (string code, string hash) = InviteCodes.New();
        StaffInvite invite = new(hash, now + InviteCodes.Lifetime);

        return await commit.ChangeAsync(
            AdministrationActions.StaffAccessReinvite,
            Target(id),
            found,
            StaffAccessErrors.NotFound,
            null,
            reinvited => Task.FromResult(reinvited.Reinvite(invite, now)),
            reinvited => Invite(reinvited, code, invite),
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<Result<StaffAccessResponse>> RevokeAsync(Guid id, CancellationToken cancellationToken)
    {
        StaffAccess? found = await FindAsync(id, cancellationToken).ConfigureAwait(false);
        Actor actor = operation.Actor;

        return await commit.ChangeAsync(
            AdministrationActions.StaffAccessRevoke,
            Target(id),
            found,
            StaffAccessErrors.NotFound,
            null,
            revoked => Task.FromResult(
                actor.Id == ActorId(revoked) ? StaffAccessErrors.RevokeSelf : revoked.Revoke(timeProvider.GetUtcNow())),
            Response,
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<Result<StaffAccessResponse>> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        StaffAccess? found = await FindAsync(id, cancellationToken).ConfigureAwait(false);
        return found is null ? StaffAccessErrors.NotFound : Response(found);
    }

    public async Task<IReadOnlyList<StaffAccessResponse>> ListAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<StaffAccess> found = await access.ListAsync(cancellationToken).ConfigureAwait(false);
        return [.. found.OrderBy(entry => entry.DisplayName, StringComparer.OrdinalIgnoreCase).ThenBy(entry => entry.Id.Value).Select(Response)];
    }

    // An empty GUID cannot be an access ID, so it names nobody.
    private Task<StaffAccess?> FindAsync(Guid id, CancellationToken cancellationToken) =>
        id == Guid.Empty ? Task.FromResult<StaffAccess?>(null) : access.GetByIdAsync(new StaffAccessId(id), cancellationToken);

    private static string ActorId(StaffAccess entry) => entry.StaffMemberId?.ToString() ?? entry.Id.ToString();

    internal static AuditTarget Target(Guid id) => new(nameof(StaffAccess), id.ToString());

    internal static StaffInviteResponse Invite(StaffAccess entry, string code, StaffInvite invite) => new(Response(entry), code, invite.ExpiresAt);

    internal static StaffAccessResponse Response(StaffAccess entry) => new(
        entry.Id.Value,
        entry.DisplayName,
        entry.Role,
        entry.StaffMemberId,
        (ContractStatus)entry.Status,
        entry.InviteExpiresAt,
        entry.LinkedAt,
        entry.RevokedAt,
        entry.Version);
}

/// <summary>
/// Errors of staff access.
/// </summary>
internal static class StaffAccessErrors
{
    public static Error NotFound { get; } = Error.NotFound("access.not_found", "There is no such access.");

    public static Error StaffMemberTaken { get; } = Error.Conflict("access.staff_member_taken", "This staff member already has access; send them a new invite instead.");

    public static Error RevokeSelf { get; } = Error.Forbidden("access.revoke_self", "Administrators cannot revoke their own access.");

    public static Error InviteNotFound { get; } = Error.NotFound("access.invite_not_found", "No open invite has this code.");

    public static Error AlreadyLinked { get; } = Error.Conflict("access.already_linked", "This sign-in already has access; sign in with another account to redeem this invite.");

    public static Error TenantHasAdmin { get; } = Error.Conflict("access.tenant_has_admin", "This tenant already has an administrator, who invites everyone else.");

    public static Error OtherTenant { get; } = Error.Forbidden("access.other_tenant", "This sign-in acts in another tenant.");
}
