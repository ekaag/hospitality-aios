using HospitalityOS.Domain.Services.Services;
using HospitalityOS.Domain.Services.Services.Events;
using HospitalityOS.SharedKernel.Ids;
using HospitalityOS.SharedKernel.Results;
using HospitalityOS.SharedKernel.Tenancy;
using Xunit;

namespace HospitalityOS.UnitTests.Domain.Services.Services;

public sealed class ServiceTests
{
    private static readonly ServiceId id = new(Guid.Parse("01890a5d-ac96-774b-bcce-b30209908001"));
    private static readonly TenantId tenantId = new(Guid.Parse("01890a5d-ac96-774b-bcce-b30209908002"));
    private static readonly PropertyId propertyId = new(Guid.Parse("01890a5d-ac96-774b-bcce-b30209908003"));
    private static readonly DateTimeOffset now = new(2026, 10, 24, 12, 0, 0, TimeSpan.Zero);

    // Demo 5: "Send two towels to my room."
    private static readonly ServiceDetails housekeepingAmenities = new(
        "housekeeping.amenities",
        "Housekeeping amenities",
        ServiceCategory.Housekeeping,
        "Towels, pillows, and toiletries delivered to your room.",
        IsGuestRequestable: true,
        Items: [new ServiceItem("bath_towel", "Bath towel", 4), new ServiceItem("pillow", "Pillow", 2)],
        TargetCompletion: TimeSpan.FromMinutes(20));

    [Fact]
    public void Create_WithItems_CreatesServiceAndRaisesCreated()
    {
        Service service = Service.Create(id, tenantId, propertyId, housekeepingAmenities, now).Value;

        Assert.Equal(housekeepingAmenities, service.Details);
        Assert.Equal([new ServiceCreated(id, propertyId, housekeepingAmenities, now)], service.DomainEvents);
    }

    [Fact]
    public void Create_NormalizesCodesToLowerCase()
    {
        ServiceDetails shouting = housekeepingAmenities with { Code = " Housekeeping.Amenities ", Items = [new ServiceItem("BATH_TOWEL", " Bath towel ", 4)] };

        Service service = Service.Create(id, tenantId, propertyId, shouting, now).Value;

        Assert.Equal("housekeeping.amenities", service.Details.Code);
        Assert.Equal([new ServiceItem("bath_towel", "Bath towel", 4)], service.Details.Items!);
    }

    [Fact]
    public void Create_WithoutItems_IsAllowedForDescribedRequests()
    {
        ServiceDetails maintenance = new("maintenance.repair", "Maintenance", ServiceCategory.Maintenance);

        Service service = Service.Create(id, tenantId, propertyId, maintenance, now).Value;

        Assert.Empty(service.Details.Items!);
    }

    [Theory]
    [InlineData("house keeping")]
    [InlineData("housekeeping..amenities")]
    [InlineData(".housekeeping")]
    [InlineData("housekeeping-")]
    [InlineData("hôtel")]
    public void Create_WithMalformedCode_ReturnsValidationError(string code)
    {
        Result<Service> result = Service.Create(id, tenantId, propertyId, housekeepingAmenities with { Code = code }, now);

        Assert.Equal([("code", "service.code_invalid")], FieldCodes(result));
    }

    [Fact]
    public void Create_WithInvalidItemsAndTarget_ReportsAll()
    {
        ServiceDetails invalid = housekeepingAmenities with
        {
            Items = [new ServiceItem("bath_towel", "Bath towel", 0), new ServiceItem("pillow", "", 2), new ServiceItem("pillow", "Pillow", 2)],
            TargetCompletion = TimeSpan.Zero,
        };

        Result<Service> result = Service.Create(id, tenantId, propertyId, invalid, now);

        Assert.Equal(
            [
                ("items[0].maxQuantityPerRequest", "service.item_max_quantity_out_of_range"),
                ("items[1].name", "service.item_name_required"),
                ("items[2].code", "service.item_code_duplicate"),
                ("targetCompletion", "service.target_completion_out_of_range"),
            ],
            FieldCodes(result));
    }

    [Fact]
    public void FindItem_MatchesCodeCaseInsensitively()
    {
        Service service = Service.Create(id, tenantId, propertyId, housekeepingAmenities, now).Value;

        Assert.Equal(new ServiceItem("bath_towel", "Bath towel", 4), service.FindItem(" Bath_Towel "));
        Assert.Null(service.FindItem("bathrobe"));
    }

    [Fact]
    public void Update_WithChangedItems_RaisesUpdated()
    {
        Service service = Service.Create(id, tenantId, propertyId, housekeepingAmenities, now).Value;
        service.DequeueDomainEvents();
        ServiceDetails withRobes = housekeepingAmenities with { Items = [.. housekeepingAmenities.Items!, new ServiceItem("bathrobe", "Bathrobe", 2)] };

        Result result = service.Update(withRobes, now.AddHours(1));

        Assert.True(result.IsSuccess);
        Assert.Equal([new ServiceUpdated(id, withRobes, now.AddHours(1))], service.DomainEvents);
    }

    [Fact]
    public void Update_WithSameDefinition_IsNoOp()
    {
        Service service = Service.Create(id, tenantId, propertyId, housekeepingAmenities, now).Value;
        service.DequeueDomainEvents();

        Result result = service.Update(housekeepingAmenities with { Name = " Housekeeping amenities " }, now.AddHours(1));

        Assert.True(result.IsSuccess);
        Assert.Empty(service.DomainEvents);
    }

    private static List<(string Field, string Code)> FieldCodes(Result result)
    {
        ValidationError error = Assert.IsType<ValidationError>(result.Error);

        return [.. error.Fields.Select(field => (field.Field, field.Code))];
    }
}
