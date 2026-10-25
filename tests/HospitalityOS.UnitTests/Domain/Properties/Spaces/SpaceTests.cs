using HospitalityOS.Domain.Properties.Spaces;
using HospitalityOS.Domain.Properties.Spaces.Events;
using HospitalityOS.SharedKernel.Ids;
using HospitalityOS.SharedKernel.Results;
using HospitalityOS.UnitTests.Domain.Properties.TestDoubles;
using Xunit;

namespace HospitalityOS.UnitTests.Domain.Properties.Spaces;

public sealed class SpaceTests
{
    private static readonly SpaceId id = new(Guid.Parse("01890a5d-ac96-774b-bcce-b30209901001"));

    [Fact]
    public void Create_TopLevel_CreatesSpaceAndRaisesCreated()
    {
        SpaceDetails details = new("Main Building", SpaceKind.Building, "MB", "Guest rooms and lobby.", "Facing the harbour.");

        Space space = Space.Create(id, AuroraGrand.TenantId, AuroraGrand.PropertyId, null, details, AuroraGrand.Now).Value;

        Assert.Equal(AuroraGrand.PropertyId, space.PropertyId);
        Assert.Null(space.ParentSpaceId);
        Assert.Equal(details, space.Details);
        Assert.Equal([new SpaceCreated(id, AuroraGrand.PropertyId, null, details, AuroraGrand.Now)], space.DomainEvents);
    }

    [Fact]
    public void Create_UnderParent_BuildsHierarchyDownToRoom()
    {
        Space building = AuroraGrand.Space("Main Building", SpaceKind.Building, idSuffix: 1);
        Space floor = AuroraGrand.Space("Floor 4", SpaceKind.Floor, building, idSuffix: 2);

        Space room = Space.Create(id, AuroraGrand.TenantId, AuroraGrand.PropertyId, floor, new SpaceDetails("Room 412", SpaceKind.Room, "412"), AuroraGrand.Now).Value;

        Assert.Equal(building.Id, floor.ParentSpaceId);
        Assert.Equal(floor.Id, room.ParentSpaceId);
    }

    [Fact]
    public void Create_UnderParentInOtherProperty_ReturnsValidationError()
    {
        Space otherBuilding = AuroraGrand.Space("Annex", SpaceKind.Building, propertyId: AuroraGrand.OtherPropertyId);

        Result<Space> result = Space.Create(id, AuroraGrand.TenantId, AuroraGrand.PropertyId, otherBuilding, new SpaceDetails("Floor 1", SpaceKind.Floor), AuroraGrand.Now);

        Assert.Equal([("parentSpaceId", "space.parent_other_property")], FieldCodes(result));
    }

    [Fact]
    public void Create_WithInvalidFields_ReportsAll()
    {
        SpaceDetails invalid = new(" ", (SpaceKind)99, new string('x', 33));

        Result<Space> result = Space.Create(id, AuroraGrand.TenantId, AuroraGrand.PropertyId, null, invalid, AuroraGrand.Now);

        Assert.Equal(
            [("name", "space.name_required"), ("code", "space.code_too_long"), ("kind", "space.kind_invalid")],
            FieldCodes(result));
    }

    [Fact]
    public void Create_WithUntrimmedValues_Normalizes()
    {
        SpaceDetails messy = new(" Pool Deck ", SpaceKind.Area, " ", " Heated pool. ", " Level 3, east elevators. ");

        Space space = Space.Create(id, AuroraGrand.TenantId, AuroraGrand.PropertyId, null, messy, AuroraGrand.Now).Value;

        Assert.Equal(new SpaceDetails("Pool Deck", SpaceKind.Area, null, "Heated pool.", "Level 3, east elevators."), space.Details);
    }

    [Fact]
    public void Update_WithChangedDetails_ReplacesAndRaisesUpdated()
    {
        Space space = AuroraGrand.Space("Pool Deck", SpaceKind.Area);
        SpaceDetails changed = new("Rooftop Pool Deck", SpaceKind.Outdoor, Directions: "Take the east elevators to the roof.");

        Result result = space.Update(changed, AuroraGrand.Now.AddHours(1));

        Assert.True(result.IsSuccess);
        Assert.Equal(changed, space.Details);
        Assert.Equal([new SpaceUpdated(space.Id, changed, AuroraGrand.Now.AddHours(1))], space.DomainEvents);
    }

    [Fact]
    public void Update_WithSameDetails_IsNoOp()
    {
        Space space = AuroraGrand.Space("Pool Deck", SpaceKind.Area);

        Result result = space.Update(new SpaceDetails(" Pool Deck ", SpaceKind.Area), AuroraGrand.Now.AddHours(1));

        Assert.True(result.IsSuccess);
        Assert.Empty(space.DomainEvents);
    }

    [Fact]
    public void Update_WithInvalidDetails_KeepsDetailsAndRaisesNothing()
    {
        Space space = AuroraGrand.Space("Pool Deck", SpaceKind.Area);

        Result result = space.Update(new SpaceDetails("", SpaceKind.Area), AuroraGrand.Now.AddHours(1));

        Assert.Equal([("name", "space.name_required")], FieldCodes(result));
        Assert.Equal("Pool Deck", space.Details.Name);
        Assert.Empty(space.DomainEvents);
    }

    private static List<(string Field, string Code)> FieldCodes(Result result)
    {
        ValidationError error = Assert.IsType<ValidationError>(result.Error);

        return [.. error.Fields.Select(field => (field.Field, field.Code))];
    }
}
