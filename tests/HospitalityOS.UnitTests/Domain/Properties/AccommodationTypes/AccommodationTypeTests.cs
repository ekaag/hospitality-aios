using HospitalityOS.Domain.Properties.AccommodationTypes;
using HospitalityOS.Domain.Properties.AccommodationTypes.Events;
using HospitalityOS.SharedKernel.Results;
using HospitalityOS.UnitTests.Domain.Properties.TestDoubles;
using Xunit;

namespace HospitalityOS.UnitTests.Domain.Properties.AccommodationTypes;

public sealed class AccommodationTypeTests
{
    private static readonly AccommodationTypeId id = new(Guid.Parse("01890a5d-ac96-774b-bcce-b30209902001"));

    [Fact]
    public void Create_WithValidDetails_CreatesTypeAndRaisesCreated()
    {
        AccommodationTypeDetails details = new("Deluxe King", 2, "King bed, harbour view.");

        AccommodationType type = AccommodationType.Create(id, AuroraGrand.TenantId, AuroraGrand.PropertyId, details, AuroraGrand.Now).Value;

        Assert.Equal(AuroraGrand.PropertyId, type.PropertyId);
        Assert.Equal(details, type.Details);
        Assert.Equal([new AccommodationTypeCreated(id, AuroraGrand.PropertyId, details, AuroraGrand.Now)], type.DomainEvents);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(AccommodationType.MaxAllowedOccupancy + 1)]
    public void Create_WithOccupancyOutOfRange_ReturnsValidationError(int maxOccupancy)
    {
        Result<AccommodationType> result = AccommodationType.Create(
            id, AuroraGrand.TenantId, AuroraGrand.PropertyId, new AccommodationTypeDetails("Deluxe King", maxOccupancy), AuroraGrand.Now);

        Assert.Equal([("maxOccupancy", "accommodation_type.max_occupancy_out_of_range")], FieldCodes(result));
    }

    [Fact]
    public void Create_WithSeveralInvalidFields_ReportsAll()
    {
        Result<AccommodationType> result = AccommodationType.Create(
            id, AuroraGrand.TenantId, AuroraGrand.PropertyId, new AccommodationTypeDetails("", 0), AuroraGrand.Now);

        Assert.Equal(
            [("name", "accommodation_type.name_required"), ("maxOccupancy", "accommodation_type.max_occupancy_out_of_range")],
            FieldCodes(result));
    }

    [Fact]
    public void Update_WithChangedDetails_ReplacesAndRaisesUpdated()
    {
        AccommodationType type = AuroraGrand.DeluxeKing();
        AccommodationTypeDetails changed = new("Deluxe King", 3, "King bed and sofa bed.");

        Result result = type.Update(changed, AuroraGrand.Now.AddHours(1));

        Assert.True(result.IsSuccess);
        Assert.Equal([new AccommodationTypeUpdated(type.Id, changed, AuroraGrand.Now.AddHours(1))], type.DomainEvents);
    }

    [Fact]
    public void Update_WithSameDetails_IsNoOp()
    {
        AccommodationType type = AuroraGrand.DeluxeKing();

        Result result = type.Update(new AccommodationTypeDetails(" Deluxe King ", 2, " "), AuroraGrand.Now.AddHours(1));

        Assert.True(result.IsSuccess);
        Assert.Empty(type.DomainEvents);
    }

    private static List<(string Field, string Code)> FieldCodes(Result result)
    {
        ValidationError error = Assert.IsType<ValidationError>(result.Error);

        return [.. error.Fields.Select(field => (field.Field, field.Code))];
    }
}
