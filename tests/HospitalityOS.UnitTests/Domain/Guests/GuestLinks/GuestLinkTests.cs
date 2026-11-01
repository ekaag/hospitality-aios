using HospitalityOS.Domain.Guests.GuestLinks;
using HospitalityOS.Domain.Guests.GuestLinks.Events;
using HospitalityOS.Domain.Guests.Guests;
using HospitalityOS.Domain.Guests.StayContexts;
using HospitalityOS.SharedKernel.Ids;
using HospitalityOS.SharedKernel.Results;
using HospitalityOS.SharedKernel.Tenancy;
using Xunit;

namespace HospitalityOS.UnitTests.Domain.Guests.GuestLinks;

public sealed class GuestLinkTests
{
    private static readonly GuestLinkId id = new(Guid.Parse("01890a5d-ac96-774b-bcce-b302099a7001"));
    private static readonly TenantId tenantId = new(Guid.Parse("01890a5d-ac96-774b-bcce-b302099a7002"));
    private static readonly PropertyId propertyId = new(Guid.Parse("01890a5d-ac96-774b-bcce-b302099a7003"));
    private static readonly DateTimeOffset now = new(2026, 11, 1, 9, 0, 0, TimeSpan.Zero);
    private static readonly string hash = new('a', 64);

    [Fact]
    public void Create_ForAnActiveStay_NamesItsGuestAndProperty_AndRaisesCreated()
    {
        StayContext stay = Stay();

        GuestLink link = GuestLink.Create(id, stay, hash, now).Value;

        Assert.Equal((tenantId, propertyId, stay.Id, stay.GuestId, hash, true), (link.TenantId, link.PropertyId, link.StayContextId, link.GuestId, link.CodeHash, link.IsOpen));
        Assert.Equal([new GuestLinkCreated(id, stay.Id, propertyId, now)], link.DomainEvents);
    }

    [Fact]
    public void Create_ForAStayThatHasEnded_IsRefused()
    {
        StayContext checkedOut = Stay();
        Assert.True(checkedOut.CheckIn(new AccommodationUnitId(Guid.CreateVersion7()), now).IsSuccess);
        Assert.True(checkedOut.CheckOut(now).IsSuccess);
        StayContext cancelled = Stay();
        cancelled.Cancel(now);

        Assert.Equal("guest_link.stay_ended", GuestLink.Create(id, checkedOut, hash, now).Error.Code);
        Assert.Equal("guest_link.stay_ended", GuestLink.Create(id, cancelled, hash, now).Error.Code);
        Assert.Equal("guest_link.code_hash_invalid", Assert.Single(((ValidationError)GuestLink.Create(id, Stay(), "ABC", now).Error).Fields).Code);
    }

    [Fact]
    public void Revoke_DropsTheCode_Once()
    {
        GuestLink link = GuestLink.Create(id, Stay(), hash, now).Value;
        link.DequeueDomainEvents();

        Assert.True(link.Revoke(now).IsSuccess);
        Assert.True(link.Revoke(now.AddHours(1)).IsSuccess);

        Assert.Equal((false, null, now), (link.IsOpen, link.CodeHash, link.RevokedAt));
        Assert.Equal([new GuestLinkRevoked(id, link.StayContextId, now)], link.DomainEvents);
    }

    private static StayContext Stay()
    {
        Guest guest = Guest.Register(new GuestId(Guid.CreateVersion7()), tenantId, new GuestProfile("Alex"), now).Value;
        return StayContext.Create(new StayContextId(Guid.CreateVersion7()), guest, propertyId, new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 4), null, now).Value;
    }
}
