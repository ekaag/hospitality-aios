using HospitalityOS.Domain.Organizations.Access;
using HospitalityOS.Domain.Organizations.Access.Events;
using HospitalityOS.SharedKernel.Execution;
using HospitalityOS.SharedKernel.Ids;
using HospitalityOS.SharedKernel.Results;
using HospitalityOS.SharedKernel.Tenancy;
using Xunit;

namespace HospitalityOS.UnitTests.Domain.Organizations.Access;

public sealed class StaffAccessTests
{
    private const string Issuer = "https://signin.example/v2.0";

    private static readonly StaffAccessId id = new(Guid.Parse("01890a5d-ac96-774b-bcce-b302099a9001"));
    private static readonly TenantId tenantId = new(Guid.Parse("01890a5d-ac96-774b-bcce-b302099a9002"));
    private static readonly StaffMemberId sam = new(Guid.Parse("01890a5d-ac96-774b-bcce-b302099a9003"));
    private static readonly DateTimeOffset now = new(2026, 11, 1, 9, 0, 0, TimeSpan.Zero);
    private static readonly StaffInvite invite = new(new string('a', 64), now.AddDays(7));

    [Fact]
    public void Create_InvitesThem_AndRaisesGranted()
    {
        StaffAccess access = StaffAccess.Create(id, tenantId, " Sam Patel ", StaffRole.Worker, sam, invite, now).Value;

        Assert.Equal(("Sam Patel", StaffAccessStatus.Invited, invite.CodeHash, invite.ExpiresAt), (access.DisplayName, access.Status, access.InviteCodeHash, access.InviteExpiresAt));
        Assert.Equal([new StaffAccessGranted(id, StaffRole.Worker, sam, now)], access.DomainEvents);
    }

    [Fact]
    public void Create_ReportsEveryProblem()
    {
        Result<StaffAccess> result = StaffAccess.Create(id, tenantId, " ", StaffRole.Worker, null, new StaffInvite("ABC", now), now);

        Assert.Equal(
            ["access.display_name_required", "access.staff_member_required", "access.invite_hash_invalid", "access.invite_expiry_invalid"],
            ((ValidationError)result.Error).Fields.Select(field => field.Code));
    }

    [Fact]
    public void Create_RefusesAnInviteOpenLongerThanTheLimit()
    {
        Result<StaffAccess> result = StaffAccess.Create(id, tenantId, "Dana", StaffRole.Manager, null, invite with { ExpiresAt = now + StaffAccess.MaxInviteLifetime + TimeSpan.FromSeconds(1) }, now);

        Assert.Equal(["access.invite_expiry_invalid"], ((ValidationError)result.Error).Fields.Select(field => field.Code));
    }

    [Fact]
    public void Redeem_LinksTheSignIn_ClosesTheInvite_AndRaisesLinked()
    {
        StaffAccess access = Invited();

        Assert.True(access.Redeem(invite.CodeHash, Issuer, "dana", now.AddDays(1)).IsSuccess);

        Assert.Equal((StaffAccessStatus.Active, Issuer, "dana", now.AddDays(1)), (access.Status, access.SignInIssuer, access.SignInSubject, access.LinkedAt));
        Assert.Equal((null, null), (access.InviteCodeHash, access.InviteExpiresAt));
        Assert.Equal([new StaffAccessLinked(id, now.AddDays(1))], access.DomainEvents);
        Assert.Equal("access.invite_not_found", access.Redeem(invite.CodeHash, Issuer, "other", now.AddDays(1)).Error.Code);
    }

    [Fact]
    public void Redeem_RefusesAnotherCode_AnExpiredInvite_RevokedAccess_AndAMissingSignIn()
    {
        Assert.Equal("access.invite_not_found", Invited().Redeem(new string('b', 64), Issuer, "dana", now).Error.Code);
        Assert.Equal("access.invite_expired", Invited().Redeem(invite.CodeHash, Issuer, "dana", invite.ExpiresAt).Error.Code);
        StaffAccess revoked = Invited();
        revoked.Revoke(now);
        Assert.Equal("access.revoked", revoked.Redeem(invite.CodeHash, Issuer, "dana", now).Error.Code);
        Assert.Equal(
            ["access.issuer_required", "access.subject_required"],
            ((ValidationError)Invited().Redeem(invite.CodeHash, " ", "", now).Error).Fields.Select(field => field.Code));
    }

    [Fact]
    public void Reinvite_UnlinksTheSignIn_OpensTheNewInvite_AndRaisesReinvited()
    {
        StaffAccess access = Invited();
        access.Redeem(invite.CodeHash, Issuer, "dana", now);
        access.DequeueDomainEvents();
        StaffInvite next = new(new string('c', 64), now.AddDays(3));

        Assert.True(access.Reinvite(next, now).IsSuccess);

        Assert.Equal((StaffAccessStatus.Invited, null, null, null), (access.Status, access.SignInIssuer, access.SignInSubject, access.LinkedAt));
        Assert.Equal(next.CodeHash, access.InviteCodeHash);
        Assert.Equal([new StaffAccessReinvited(id, now)], access.DomainEvents);
    }

    [Fact]
    public void Revoke_EndsEverything_Once()
    {
        StaffAccess access = Invited();
        access.Redeem(invite.CodeHash, Issuer, "dana", now);
        access.DequeueDomainEvents();

        Assert.True(access.Revoke(now).IsSuccess);
        Assert.True(access.Revoke(now.AddDays(1)).IsSuccess);

        Assert.Equal((StaffAccessStatus.Revoked, now, null, null, null), (access.Status, access.RevokedAt, access.SignInSubject, access.InviteCodeHash, access.InviteExpiresAt));
        Assert.Equal([new StaffAccessRevoked(id, now)], access.DomainEvents);
        Assert.Equal("access.revoked", access.Reinvite(invite, now).Error.Code);
    }

    private static StaffAccess Invited()
    {
        StaffAccess access = StaffAccess.Create(id, tenantId, "Dana Reyes", StaffRole.Manager, null, invite, now).Value;
        access.DequeueDomainEvents();
        return access;
    }
}
