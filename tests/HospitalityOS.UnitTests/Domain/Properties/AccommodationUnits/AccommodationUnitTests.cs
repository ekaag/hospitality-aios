using HospitalityOS.Domain.Properties.AccommodationTypes;
using HospitalityOS.Domain.Properties.AccommodationUnits;
using HospitalityOS.Domain.Properties.AccommodationUnits.Events;
using HospitalityOS.Domain.Properties.Spaces;
using HospitalityOS.SharedKernel.Ids;
using HospitalityOS.SharedKernel.Results;
using HospitalityOS.UnitTests.Domain.Properties.TestDoubles;
using Xunit;

namespace HospitalityOS.UnitTests.Domain.Properties.AccommodationUnits;

public sealed class AccommodationUnitTests
{
    private static readonly AccommodationUnitId id = new(Guid.Parse("01890a5d-ac96-774b-bcce-b30209903001"));

    [Fact]
    public void Create_Room412_LinksTypeAndSpaceAndRaisesCreated()
    {
        AccommodationType deluxeKing = AuroraGrand.DeluxeKing();
        Space room412 = AuroraGrand.Space("Room 412", SpaceKind.Room);

        AccommodationUnit unit = AccommodationUnit.Create(id, deluxeKing, room412, " 412 ", AuroraGrand.Now).Value;

        Assert.Equal(AuroraGrand.TenantId, unit.TenantId);
        Assert.Equal(AuroraGrand.PropertyId, unit.PropertyId);
        Assert.Equal(deluxeKing.Id, unit.AccommodationTypeId);
        Assert.Equal(room412.Id, unit.SpaceId);
        Assert.Equal("412", unit.UnitNumber);
        Assert.Equal(
            [new AccommodationUnitCreated(id, AuroraGrand.PropertyId, deluxeKing.Id, room412.Id, "412", AuroraGrand.Now)],
            unit.DomainEvents);
    }

    [Fact]
    public void Create_WithSpaceInOtherProperty_AndMissingNumber_ReportsBoth()
    {
        AccommodationType deluxeKing = AuroraGrand.DeluxeKing();
        Space otherRoom = AuroraGrand.Space("Room 101", SpaceKind.Room, propertyId: AuroraGrand.OtherPropertyId);

        Result<AccommodationUnit> result = AccommodationUnit.Create(id, deluxeKing, otherRoom, "", AuroraGrand.Now);

        Assert.Equal(
            [("unitNumber", "accommodation_unit.unit_number_required"), ("spaceId", "accommodation_unit.space_other_property")],
            FieldCodes(result));
    }

    [Fact]
    public void Renumber_WithNewNumber_UpdatesAndRaisesUpdated()
    {
        AccommodationUnit unit = CreateRoom412();

        Result result = unit.Renumber("412A", AuroraGrand.Now.AddHours(1));

        Assert.True(result.IsSuccess);
        Assert.Equal("412A", unit.UnitNumber);
        Assert.Equal([new AccommodationUnitUpdated(id, unit.AccommodationTypeId, "412A", AuroraGrand.Now.AddHours(1))], unit.DomainEvents);
    }

    [Fact]
    public void Renumber_WithSameNumber_IsNoOp()
    {
        AccommodationUnit unit = CreateRoom412();

        Result result = unit.Renumber(" 412 ", AuroraGrand.Now.AddHours(1));

        Assert.True(result.IsSuccess);
        Assert.Empty(unit.DomainEvents);
    }

    [Fact]
    public void Renumber_WithTooLongNumber_ReturnsValidationError()
    {
        AccommodationUnit unit = CreateRoom412();

        Result result = unit.Renumber(new string('9', AccommodationUnit.MaxUnitNumberLength + 1), AuroraGrand.Now.AddHours(1));

        Assert.Equal([("unitNumber", "accommodation_unit.unit_number_too_long")], FieldCodes(result));
        Assert.Equal("412", unit.UnitNumber);
    }

    [Fact]
    public void ChangeType_ToTypeInSameProperty_UpdatesAndRaisesUpdated()
    {
        AccommodationUnit unit = CreateRoom412();
        AccommodationType suite = AuroraGrand.DeluxeKing(idSuffix: 201);

        Result result = unit.ChangeType(suite, AuroraGrand.Now.AddHours(1));

        Assert.True(result.IsSuccess);
        Assert.Equal(suite.Id, unit.AccommodationTypeId);
        Assert.Equal([new AccommodationUnitUpdated(id, suite.Id, "412", AuroraGrand.Now.AddHours(1))], unit.DomainEvents);
    }

    [Fact]
    public void ChangeType_ToSameType_IsNoOp()
    {
        AccommodationType deluxeKing = AuroraGrand.DeluxeKing();
        AccommodationUnit unit = CreateRoom412(deluxeKing);

        Result result = unit.ChangeType(deluxeKing, AuroraGrand.Now.AddHours(1));

        Assert.True(result.IsSuccess);
        Assert.Empty(unit.DomainEvents);
    }

    [Fact]
    public void ChangeType_ToTypeInOtherProperty_ReturnsValidationError()
    {
        AccommodationUnit unit = CreateRoom412();
        AccommodationType otherType = AuroraGrand.DeluxeKing(AuroraGrand.OtherPropertyId, idSuffix: 202);

        Result result = unit.ChangeType(otherType, AuroraGrand.Now.AddHours(1));

        Assert.Equal([("accommodationTypeId", "accommodation_unit.type_other_property")], FieldCodes(result));
        Assert.Empty(unit.DomainEvents);
    }

    private static AccommodationUnit CreateRoom412(AccommodationType? type = null)
    {
        AccommodationUnit unit = AccommodationUnit
            .Create(id, type ?? AuroraGrand.DeluxeKing(), AuroraGrand.Space("Room 412", SpaceKind.Room), "412", AuroraGrand.Now)
            .Value;
        unit.DequeueDomainEvents();

        return unit;
    }

    private static List<(string Field, string Code)> FieldCodes(Result result)
    {
        ValidationError error = Assert.IsType<ValidationError>(result.Error);

        return [.. error.Fields.Select(field => (field.Field, field.Code))];
    }
}
