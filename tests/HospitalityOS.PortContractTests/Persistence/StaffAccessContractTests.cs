using HospitalityOS.Domain.Organizations.Access;
using HospitalityOS.PortContractTests.Persistence.Support;
using HospitalityOS.Ports.Persistence;
using HospitalityOS.SharedKernel.Execution;
using HospitalityOS.SharedKernel.Results;
using Xunit;

namespace HospitalityOS.PortContractTests.Persistence;

/// <summary>
/// Staff access across tenants (K4b): the directory finds a sign-in or an open invite whichever tenant holds it, and
/// nothing revoked or closed; a sign-in, an invite code, and a staff member each belong to one access, even when two
/// scopes race (P8).
/// </summary>
public abstract class StaffAccessContractTests : PersistenceContractTests
{
    private const string Issuer = "https://issuer.example/v2.0";

    [Fact]
    public async Task Directory_FindsALinkedSignIn_InWhicheverTenantHoldsIt()
    {
        StaffAccess worker = TestData.StaffAccess(TestData.TenantB, 1, TestData.Maria);
        worker.Redeem(worker.InviteCodeHash!, Issuer, "maria", TestData.Now);
        await SaveAsync(worker);
        await using PersistenceSession session = OpenSession(TestData.TenantA);

        StaffSignIn? found = await session.Get<IStaffAccessDirectory>().FindBySignInAsync(Issuer, "maria", Cancellation);

        Assert.Equal(new StaffSignIn(TestData.TenantB, worker.Id, StaffRole.Worker, TestData.Maria), found);
        Assert.Null(await session.Get<IStaffAccessDirectory>().FindBySignInAsync(Issuer + "/other", "maria", Cancellation));
        Assert.Null(await session.Get<IStaffAccessDirectory>().FindBySignInAsync(Issuer, "MARIA", Cancellation));
    }

    [Fact]
    public async Task Directory_FindsNoSignIn_ForRevokedOrUnlinkedAccess()
    {
        StaffAccess revoked = TestData.StaffAccess(TestData.TenantA, 1);
        revoked.Redeem(revoked.InviteCodeHash!, Issuer, "dana", TestData.Now);
        revoked.Revoke(TestData.Now);
        await SaveAsync(revoked, TestData.StaffAccess(TestData.TenantA, 2));
        await using PersistenceSession session = OpenSession();

        Assert.Null(await session.Get<IStaffAccessDirectory>().FindBySignInAsync(Issuer, "dana", Cancellation));
    }

    [Fact]
    public async Task Directory_FindsAnOpenInvite_InWhicheverTenantHoldsIt_UntilRedeemed()
    {
        StaffAccess invited = TestData.StaffAccess(TestData.TenantB, 1);
        await SaveAsync(invited);
        string hash = invited.InviteCodeHash!;

        await using (PersistenceSession session = OpenSession(TestData.TenantA))
        {
            Assert.Equal(new StaffAccessEntry(TestData.TenantB, invited.Id), await session.Get<IStaffAccessDirectory>().FindByInviteAsync(hash, Cancellation));
        }

        await using (PersistenceSession session = OpenSession(TestData.TenantB))
        {
            StaffAccess loaded = (await session.Get<IStaffAccessRepository>().GetByIdAsync(invited.Id, Cancellation))!;
            Assert.True(loaded.Redeem(hash, Issuer, "dana", TestData.Now).IsSuccess);
            Assert.True((await session.CommitAsync(Cancellation)).IsSuccess);
        }

        await using PersistenceSession after = OpenSession(TestData.TenantB);
        Assert.Null(await after.Get<IStaffAccessDirectory>().FindByInviteAsync(hash, Cancellation));
        Assert.Equal(StaffAccessStatus.Active, (await after.Get<IStaffAccessRepository>().GetByIdAsync(invited.Id, Cancellation))!.Status);
    }

    [Fact]
    public async Task Repository_FindsAccessByStaffMember_InTheCurrentTenantOnly()
    {
        StaffAccess worker = TestData.StaffAccess(TestData.TenantA, 1, TestData.Maria);
        await SaveAsync(worker, TestData.StaffAccess(TestData.TenantA, 2));

        await using (PersistenceSession session = OpenSession(TestData.TenantA))
        {
            Assert.Equal(worker.Id, (await session.Get<IStaffAccessRepository>().FindByStaffMemberAsync(TestData.Maria, Cancellation))?.Id);
            Assert.Equal(2, (await session.Get<IStaffAccessRepository>().ListAsync(Cancellation)).Count);
        }

        await using PersistenceSession other = OpenSession(TestData.TenantB);
        Assert.Null(await other.Get<IStaffAccessRepository>().FindByStaffMemberAsync(TestData.Maria, Cancellation));
        Assert.Empty(await other.Get<IStaffAccessRepository>().ListAsync(Cancellation));
    }

    [Fact]
    public async Task Commit_SignInLinkedInTwoTenants_ReturnsDuplicateKey()
    {
        StaffAccess first = TestData.StaffAccess(TestData.TenantA, 1);
        first.Redeem(first.InviteCodeHash!, Issuer, "dana", TestData.Now);
        await SaveAsync(first);
        StaffAccess second = TestData.StaffAccess(TestData.TenantB, 2);
        second.Redeem(second.InviteCodeHash!, Issuer, "dana", TestData.Now);

        AssertDuplicateKey(await AddAndCommitAsync(second));
    }

    [Fact]
    public async Task Commit_SameSubjectFromAnotherIssuer_Succeeds()
    {
        StaffAccess first = TestData.StaffAccess(TestData.TenantA, 1);
        first.Redeem(first.InviteCodeHash!, Issuer, "dana", TestData.Now);
        await SaveAsync(first);
        StaffAccess second = TestData.StaffAccess(TestData.TenantA, 2);
        second.Redeem(second.InviteCodeHash!, Issuer + "/other", "dana", TestData.Now);

        Assert.True((await AddAndCommitAsync(second)).IsSuccess);
    }

    [Fact]
    public async Task Commit_InviteCodeInTwoTenants_ReturnsDuplicateKey()
    {
        await SaveAsync(TestData.StaffAccess(TestData.TenantA, 1, inviteSeed: 7));

        AssertDuplicateKey(await AddAndCommitAsync(TestData.StaffAccess(TestData.TenantB, 2, inviteSeed: 7)));
    }

    [Fact]
    public async Task Commit_SecondAccessForStaffMember_ReturnsDuplicateKey()
    {
        await SaveAsync(TestData.StaffAccess(TestData.TenantA, 1, TestData.Maria));

        AssertDuplicateKey(await AddAndCommitAsync(TestData.StaffAccess(TestData.TenantA, 2, TestData.Maria)));
    }

    [Fact]
    public async Task Commit_TwoScopesLinkingTheSameSignIn_SecondReturnsDuplicateKey()
    {
        StaffAccess one = TestData.StaffAccess(TestData.TenantA, 1);
        StaffAccess two = TestData.StaffAccess(TestData.TenantA, 2);
        await SaveAsync(one, two);
        await using PersistenceSession first = OpenSession();
        await using PersistenceSession second = OpenSession();
        (await first.Get<IStaffAccessRepository>().GetByIdAsync(one.Id, Cancellation))!.Redeem(one.InviteCodeHash!, Issuer, "dana", TestData.Now);
        (await second.Get<IStaffAccessRepository>().GetByIdAsync(two.Id, Cancellation))!.Redeem(two.InviteCodeHash!, Issuer, "dana", TestData.Now);
        Assert.True((await first.CommitAsync(Cancellation)).IsSuccess);

        AssertDuplicateKey(await second.CommitAsync(Cancellation));
    }

    [Fact]
    public async Task Commit_RevokedAccess_FreesItsSignInForNewAccess()
    {
        StaffAccess first = TestData.StaffAccess(TestData.TenantA, 1);
        first.Redeem(first.InviteCodeHash!, Issuer, "dana", TestData.Now);
        first.Revoke(TestData.Now);
        await SaveAsync(first);
        StaffAccess second = TestData.StaffAccess(TestData.TenantB, 2);
        second.Redeem(second.InviteCodeHash!, Issuer, "dana", TestData.Now);

        Assert.True((await AddAndCommitAsync(second)).IsSuccess);
    }

    private async Task<Result<CommitReceipt>> AddAndCommitAsync(StaffAccess access)
    {
        await using PersistenceSession session = OpenSession(access.TenantId);
        await session.Get<IStaffAccessRepository>().AddAsync(access, Cancellation);

        return await session.CommitAsync(Cancellation);
    }

    private static void AssertDuplicateKey(Result<CommitReceipt> commit)
    {
        Assert.True(commit.IsFailure);
        Assert.Equal(ErrorKind.Conflict, commit.Error.Kind);
        Assert.Equal(PersistenceErrors.DuplicateKeyCode, commit.Error.Code);
    }
}
