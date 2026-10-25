using HospitalityOS.Domain.Properties.Amenities;
using HospitalityOS.Domain.Properties.Amenities.Events;
using HospitalityOS.Domain.Properties.Spaces;
using HospitalityOS.SharedKernel.Results;
using HospitalityOS.UnitTests.Domain.Properties.TestDoubles;
using Xunit;

namespace HospitalityOS.UnitTests.Domain.Properties.Amenities;

public sealed class AmenityTests
{
    private static readonly AmenityId id = new(Guid.Parse("01890a5d-ac96-774b-bcce-b30209905001"));
    private static readonly AmenityDetails gym = new("Fitness Center", AmenityCategory.Fitness, "Cardio and free weights.");

    [Fact]
    public void Create_WithLocation_LinksSpaceAndRaisesCreated()
    {
        Space level2 = AuroraGrand.Space("Level 2 Gym", SpaceKind.Room);

        Amenity amenity = Amenity.Create(id, AuroraGrand.TenantId, AuroraGrand.PropertyId, gym, level2, AuroraGrand.Now).Value;

        Assert.Equal(AuroraGrand.PropertyId, amenity.PropertyId);
        Assert.Equal(level2.Id, amenity.LocationSpaceId);
        Assert.Equal(1, amenity.Version);
        Assert.Equal([new AmenityCreated(id, AuroraGrand.PropertyId, gym, level2.Id, AuroraGrand.Now)], amenity.DomainEvents);
    }

    [Fact]
    public void Create_WithInvalidFieldsAndLocationInOtherProperty_ReportsAll()
    {
        Space otherSpace = AuroraGrand.Space("Annex Gym", SpaceKind.Room, propertyId: AuroraGrand.OtherPropertyId);

        Result<Amenity> result = Amenity.Create(
            id, AuroraGrand.TenantId, AuroraGrand.PropertyId, new AmenityDetails(" ", (AmenityCategory)99), otherSpace, AuroraGrand.Now);

        Assert.Equal(
            [("name", "amenity.name_required"), ("category", "amenity.category_invalid"), ("locationSpaceId", "amenity.location_other_property")],
            FieldCodes(result));
    }

    [Fact]
    public void Update_WithNewDetailsAndClearedLocation_RaisesUpdated()
    {
        Amenity amenity = CreateGym(AuroraGrand.Space("Level 2 Gym", SpaceKind.Room));
        AmenityDetails changed = gym with { Name = "24h Fitness Center" };

        Result result = amenity.Update(changed, null, AuroraGrand.Now.AddHours(1));

        Assert.True(result.IsSuccess);
        Assert.Null(amenity.LocationSpaceId);
        Assert.Equal([new AmenityUpdated(id, changed, null, AuroraGrand.Now.AddHours(1))], amenity.DomainEvents);
    }

    [Fact]
    public void Update_WithSameDetailsAndLocation_IsNoOp()
    {
        Space level2 = AuroraGrand.Space("Level 2 Gym", SpaceKind.Room);
        Amenity amenity = CreateGym(level2);

        Result result = amenity.Update(gym with { Name = " Fitness Center " }, level2, AuroraGrand.Now.AddHours(1));

        Assert.True(result.IsSuccess);
        Assert.Empty(amenity.DomainEvents);
    }

    private static Amenity CreateGym(Space? location)
    {
        Amenity amenity = Amenity.Create(id, AuroraGrand.TenantId, AuroraGrand.PropertyId, gym, location, AuroraGrand.Now).Value;
        amenity.DequeueDomainEvents();

        return amenity;
    }

    private static List<(string Field, string Code)> FieldCodes(Result result)
    {
        ValidationError error = Assert.IsType<ValidationError>(result.Error);

        return [.. error.Fields.Select(field => (field.Field, field.Code))];
    }
}
