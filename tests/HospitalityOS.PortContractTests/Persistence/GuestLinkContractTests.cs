using HospitalityOS.Domain.Guests.GuestLinks;
using HospitalityOS.Domain.Guests.StayContexts;
using HospitalityOS.PortContractTests.Persistence.Support;
using HospitalityOS.Ports.Persistence;
using HospitalityOS.SharedKernel.Results;
using Xunit;

namespace HospitalityOS.PortContractTests.Persistence;

/// <summary>
/// Check-in links (K4c): the directory finds an open link by its code in whichever tenant holds it, and nothing revoked;
/// a code opens one link, even when two scopes race (P8); and the queries staff see stays and links through.
/// </summary>
public abstract class GuestLinkContractTests : PersistenceContractTests
{
    [Fact]
    public async Task Directory_FindsAnOpenLink_InWhicheverTenantHoldsIt_UntilRevoked()
    {
        GuestLink link = TestData.GuestLink(TestData.TenantB, 1);
        string hash = link.CodeHash!;
        await SaveAsync(TestData.StayContext(TestData.TenantB, 1), link);

        await using (PersistenceSession session = OpenSession(TestData.TenantA))
        {
            Assert.Equal(new GuestLinkEntry(TestData.TenantB, link.Id), await session.Get<IGuestLinkDirectory>().FindByCodeAsync(hash, Cancellation));
            Assert.Null(await session.Get<IGuestLinkDirectory>().FindByCodeAsync(new string('f', 64), Cancellation));
        }

        await using (PersistenceSession session = OpenSession(TestData.TenantB))
        {
            (await session.Get<IGuestLinkRepository>().GetByIdAsync(link.Id, Cancellation))!.Revoke(TestData.Now);
            Assert.True((await session.CommitAsync(Cancellation)).IsSuccess);
        }

        await using PersistenceSession after = OpenSession(TestData.TenantA);
        Assert.Null(await after.Get<IGuestLinkDirectory>().FindByCodeAsync(hash, Cancellation));
    }

    [Fact]
    public async Task Repository_FindsAStaysOpenLink_AndAPropertysOpenLinks_InTheCurrentTenantOnly()
    {
        StayContext stay = TestData.StayContext(TestData.TenantA, 1);
        StayContext other = TestData.StayContext(TestData.TenantA, 2);
        GuestLink revoked = TestData.GuestLink(TestData.TenantA, 1, stay);
        revoked.Revoke(TestData.Now);
        GuestLink open = TestData.GuestLink(TestData.TenantA, 2, stay);
        GuestLink elsewhere = TestData.GuestLink(TestData.TenantA, 3, TestData.StayContext(TestData.TenantA, 3, propertyId: TestData.OtherPropertyId));
        await SaveAsync(revoked, open, TestData.GuestLink(TestData.TenantA, 4, other), elsewhere);

        await using (PersistenceSession session = OpenSession(TestData.TenantA))
        {
            Assert.Equal(open.Id, (await session.Get<IGuestLinkRepository>().FindOpenForStayAsync(stay.Id, Cancellation))?.Id);
            Assert.Equal(2, (await session.Get<IGuestLinkRepository>().ListOpenForPropertyAsync(TestData.PropertyId, Cancellation)).Count);
        }

        await using PersistenceSession tenantB = OpenSession(TestData.TenantB);
        Assert.Null(await tenantB.Get<IGuestLinkRepository>().FindOpenForStayAsync(stay.Id, Cancellation));
    }

    [Fact]
    public async Task Repository_ListsAPropertysReservedAndInHouseStays_ByArrival()
    {
        StayContext inHouse = TestData.StayContext(TestData.TenantA, 1);
        inHouse.CheckIn(null, TestData.Now);
        StayContext reserved = TestData.StayContext(TestData.TenantA, 2);
        reserved.ChangeDates(new DateOnly(2026, 10, 20), new DateOnly(2026, 10, 22), TestData.Now);
        StayContext gone = TestData.StayContext(TestData.TenantA, 3);
        gone.CheckIn(null, TestData.Now);
        gone.CheckOut(TestData.Now);
        StayContext cancelled = TestData.StayContext(TestData.TenantA, 4);
        cancelled.Cancel(TestData.Now);
        await SaveAsync(inHouse, reserved, gone, cancelled, TestData.StayContext(TestData.TenantA, 5, propertyId: TestData.OtherPropertyId), TestData.StayContext(TestData.TenantB, 6));
        await using PersistenceSession session = OpenSession(TestData.TenantA);

        IReadOnlyList<StayContext> active = await session.Get<IStayContextRepository>().ListActiveForPropertyAsync(TestData.PropertyId, Cancellation);

        Assert.Equal([reserved.Id, inHouse.Id], active.Select(stay => stay.Id));
    }

    [Fact]
    public async Task Commit_ACodeInTwoTenants_ReturnsDuplicateKey()
    {
        await SaveAsync(TestData.GuestLink(TestData.TenantA, 1, codeSeed: 9));
        await using PersistenceSession session = OpenSession(TestData.TenantB);
        await session.Get<IGuestLinkRepository>().AddAsync(TestData.GuestLink(TestData.TenantB, 2, codeSeed: 9), Cancellation);

        Result<CommitReceipt> commit = await session.CommitAsync(Cancellation);

        Assert.True(commit.IsFailure);
        Assert.Equal(PersistenceErrors.DuplicateKeyCode, commit.Error.Code);
    }

    [Fact]
    public async Task Commit_RevokedLinks_HoldNoCode_SoTheirCodeCanBeIssuedAgain()
    {
        GuestLink first = TestData.GuestLink(TestData.TenantA, 1, codeSeed: 9);
        first.Revoke(TestData.Now);
        await SaveAsync(first, TestData.GuestLink(TestData.TenantA, 2, codeSeed: 9));

        await using PersistenceSession session = OpenSession();
        Assert.Null((await session.Get<IGuestLinkRepository>().GetByIdAsync(first.Id, Cancellation))!.CodeHash);
    }
}
