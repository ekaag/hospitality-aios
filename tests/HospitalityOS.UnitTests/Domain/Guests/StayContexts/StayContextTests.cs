using HospitalityOS.Domain.Guests.Guests;
using HospitalityOS.Domain.Guests.StayContexts;
using HospitalityOS.Domain.Guests.StayContexts.Events;
using HospitalityOS.SharedKernel.Ids;
using HospitalityOS.SharedKernel.Results;
using HospitalityOS.UnitTests.Domain.Guests.TestDoubles;
using Xunit;

namespace HospitalityOS.UnitTests.Domain.Guests.StayContexts;

public sealed class StayContextTests
{
    private static readonly StayContextId id = new(GuestFixture.Id(10));

    [Fact]
    public void Create_ReservesStayForGuestAndRaisesCreated()
    {
        Guest guest = GuestFixture.Guest();

        StayContext stay = StayContext.Create(id, guest, GuestFixture.PropertyId, GuestFixture.Arrival, GuestFixture.Departure, null, GuestFixture.Now).Value;

        Assert.Equal(StayStatus.Reserved, stay.Status);
        Assert.Equal(guest.Id, stay.GuestId);
        Assert.Equal(guest.TenantId, stay.TenantId);
        Assert.Equal(
            [new StayContextCreated(id, GuestFixture.PropertyId, guest.Id, GuestFixture.Arrival, GuestFixture.Departure, null, StayStatus.Reserved, GuestFixture.Now)],
            stay.DomainEvents);
    }

    [Theory]
    [InlineData(0, "stay.departure_before_arrival")]
    [InlineData(-1, "stay.departure_before_arrival")]
    [InlineData(StayContext.MaxNights + 1, "stay.too_long")]
    public void Create_WithBadDates_ReturnsValidationError(int nights, string code)
    {
        Result<StayContext> result = StayContext.Create(
            id, GuestFixture.Guest(), GuestFixture.PropertyId, GuestFixture.Arrival, GuestFixture.Arrival.AddDays(nights), null, GuestFixture.Now);

        Assert.Equal(code, Assert.Single(Assert.IsType<ValidationError>(result.Error).Fields).Code);
    }

    [Fact]
    public void CheckIn_WithUnit_PutsGuestInHouseInRoom412()
    {
        StayContext stay = GuestFixture.Stay();

        Result result = stay.CheckIn(GuestFixture.Room412, GuestFixture.Now);

        Assert.True(result.IsSuccess);
        Assert.True(stay.IsInHouse);
        Assert.Equal(GuestFixture.Room412, stay.AccommodationUnitId);
        Assert.Equal(
            [new StayContextUpdated(id, GuestFixture.Arrival, GuestFixture.Departure, GuestFixture.Room412, StayStatus.InHouse, GuestFixture.Now)],
            stay.DomainEvents);
    }

    [Fact]
    public void CheckIn_WithoutAnyUnit_ReturnsInvalidState()
    {
        StayContext stay = GuestFixture.Stay();

        Result result = stay.CheckIn(null, GuestFixture.Now);

        Assert.Equal("stay.unit_required", result.Error.Code);
        Assert.Equal(StayStatus.Reserved, stay.Status);
    }

    [Fact]
    public void CheckIn_UsingPreassignedUnit_AndRepeated_IsIdempotent()
    {
        StayContext stay = GuestFixture.Stay(unit: GuestFixture.Room412);

        stay.CheckIn(null, GuestFixture.Now);
        stay.CheckIn(null, GuestFixture.Now.AddMinutes(1));

        Assert.Single(stay.DomainEvents);
        Assert.Equal(GuestFixture.Room412, stay.AccommodationUnitId);
    }

    [Fact]
    public void AssignUnit_RoomMove_RaisesUpdated_AndSameUnitIsNoOp()
    {
        StayContext stay = GuestFixture.Stay(unit: GuestFixture.Room412);
        stay.CheckIn(null, GuestFixture.Now);
        stay.DequeueDomainEvents();

        stay.AssignUnit(GuestFixture.Room415, GuestFixture.Now.AddHours(1));
        stay.AssignUnit(GuestFixture.Room415, GuestFixture.Now.AddHours(2));

        Assert.Equal(GuestFixture.Room415, stay.AccommodationUnitId);
        Assert.Single(stay.DomainEvents);
    }

    [Fact]
    public void ChangeDates_Extension_RaisesUpdated_ButArrivalIsFixedAfterCheckIn()
    {
        StayContext stay = GuestFixture.Stay(unit: GuestFixture.Room412);
        stay.CheckIn(null, GuestFixture.Now);
        stay.DequeueDomainEvents();

        Result extend = stay.ChangeDates(GuestFixture.Arrival, GuestFixture.Departure.AddDays(2), GuestFixture.Now);
        Result moveArrival = stay.ChangeDates(GuestFixture.Arrival.AddDays(-1), GuestFixture.Departure.AddDays(2), GuestFixture.Now);

        Assert.True(extend.IsSuccess);
        Assert.Equal("stay.arrival_fixed", moveArrival.Error.Code);
        Assert.Equal(GuestFixture.Departure.AddDays(2), stay.Departure);
        Assert.Single(stay.DomainEvents);
    }

    [Fact]
    public void CheckOut_FromInHouse_EndsStay_AndBlocksFurtherChanges()
    {
        StayContext stay = GuestFixture.Stay(unit: GuestFixture.Room412);
        stay.CheckIn(null, GuestFixture.Now);

        Result checkOut = stay.CheckOut(GuestFixture.Now.AddDays(3));
        Result again = stay.CheckOut(GuestFixture.Now.AddDays(3));
        Result reCheckIn = stay.CheckIn(GuestFixture.Room412, GuestFixture.Now.AddDays(3));
        Result move = stay.AssignUnit(GuestFixture.Room415, GuestFixture.Now.AddDays(3));

        Assert.True(checkOut.IsSuccess);
        Assert.True(again.IsSuccess);
        Assert.Equal(ErrorKind.InvalidState, reCheckIn.Error.Kind);
        Assert.Equal(ErrorKind.InvalidState, move.Error.Kind);
        Assert.Equal(StayStatus.CheckedOut, stay.Status);
    }

    [Fact]
    public void CheckOut_FromReserved_ReturnsInvalidState()
    {
        StayContext stay = GuestFixture.Stay();

        Assert.Equal("stay.transition_invalid", stay.CheckOut(GuestFixture.Now).Error.Code);
    }

    [Fact]
    public void Cancel_FromReserved_Succeeds_ButNotOnceInHouse()
    {
        StayContext reserved = GuestFixture.Stay();
        StayContext inHouse = GuestFixture.Stay(unit: GuestFixture.Room412, idSuffix: 11);
        inHouse.CheckIn(null, GuestFixture.Now);

        Assert.True(reserved.Cancel(GuestFixture.Now).IsSuccess);
        Assert.Equal(StayStatus.Cancelled, reserved.Status);
        Assert.Equal("stay.transition_invalid", inHouse.Cancel(GuestFixture.Now).Error.Code);
    }
}
