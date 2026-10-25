using HospitalityOS.Domain.Properties.Properties;
using HospitalityOS.Domain.Properties.Properties.Events;
using HospitalityOS.SharedKernel.Ids;
using HospitalityOS.SharedKernel.Results;
using HospitalityOS.SharedKernel.Tenancy;
using Xunit;

namespace HospitalityOS.UnitTests.Domain.Properties.Properties;

public sealed class PropertyTests
{
    private static readonly PropertyId id = new(Guid.Parse("01890a5d-ac96-774b-bcce-b302099a8101"));
    private static readonly TenantId tenantId = new(Guid.Parse("01890a5d-ac96-774b-bcce-b302099a8102"));
    private static readonly OrganizationId organizationId = new(Guid.Parse("01890a5d-ac96-774b-bcce-b302099a8103"));
    private static readonly DateTimeOffset now = new(2026, 10, 24, 12, 0, 0, TimeSpan.Zero);

    private static readonly PostalAddress auroraAddress = new("1 Harbour Way", null, "Seattle", "WA", "98101", "US");
    private static readonly PropertyDetails auroraGrand = new("Aurora Grand", "America/Los_Angeles", auroraAddress, "+1 206 555 0100", "frontdesk@auroragrand.example");

    [Fact]
    public void Create_WithValidDetails_CreatesPropertyAndRaisesCreated()
    {
        Result<Property> result = Property.Create(id, tenantId, organizationId, auroraGrand, now);

        Property property = result.Value;
        Assert.Equal(id, property.Id);
        Assert.Equal(tenantId, property.TenantId);
        Assert.Equal(organizationId, property.OrganizationId);
        Assert.Equal(auroraGrand, property.Details);
        Assert.Equal([new PropertyCreated(id, organizationId, auroraGrand, now)], property.DomainEvents);
    }

    [Fact]
    public void Create_WithUntrimmedValues_NormalizesDetails()
    {
        PropertyDetails messy = new(
            "  Aurora Grand ",
            " America/Los_Angeles ",
            new PostalAddress(" 1 Harbour Way ", "  ", " Seattle ", " WA ", " 98101 ", " us "),
            "  ",
            null);

        Property property = Property.Create(id, tenantId, organizationId, messy, now).Value;

        Assert.Equal(
            new PropertyDetails("Aurora Grand", "America/Los_Angeles", new PostalAddress("1 Harbour Way", null, "Seattle", "WA", "98101", "US")),
            property.Details);
    }

    [Fact]
    public void Create_WithSeveralInvalidFields_ReportsAllAndCreatesNothing()
    {
        PropertyDetails invalid = new("", "Mars/Olympus_Mons", new PostalAddress("", null, "", null, null, "USA"), null, "not-an-email");

        Result<Property> result = Property.Create(id, tenantId, organizationId, invalid, now);

        Assert.Equal(
            [
                ("name", "property.name_required"),
                ("timeZone", "property.time_zone_invalid"),
                ("address.line1", "property.address_line1_required"),
                ("address.city", "property.address_city_required"),
                ("address.countryCode", "property.address_country_code_invalid"),
                ("contactEmail", "property.contact_email_invalid"),
            ],
            FieldCodes(result));
    }

    [Theory]
    [InlineData("", "property.time_zone_required")]
    [InlineData("Not/AZone", "property.time_zone_invalid")]
    [InlineData("Pacific Standard Time", "property.time_zone_invalid")]
    public void Create_WithBadTimeZone_ReturnsValidationError(string timeZoneId, string code)
    {
        Result<Property> result = Property.Create(id, tenantId, organizationId, auroraGrand with { TimeZoneId = timeZoneId }, now);

        Assert.Equal([("timeZone", code)], FieldCodes(result));
    }

    [Theory]
    [InlineData("Europe/London")]
    [InlineData("Asia/Kolkata")]
    [InlineData("UTC")]
    public void Create_WithIanaTimeZone_Succeeds(string timeZoneId)
    {
        Result<Property> result = Property.Create(id, tenantId, organizationId, auroraGrand with { TimeZoneId = timeZoneId }, now);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void Create_WithMissingAddress_ReturnsValidationError()
    {
        Result<Property> result = Property.Create(id, tenantId, organizationId, auroraGrand with { Address = null! }, now);

        Assert.Equal([("address", "property.address_required")], FieldCodes(result));
    }

    [Theory]
    [InlineData("frontdesk")]
    [InlineData("@auroragrand.example")]
    [InlineData("frontdesk@")]
    [InlineData("front desk@auroragrand.example")]
    [InlineData("a@b@auroragrand.example")]
    public void Create_WithInvalidEmail_ReturnsValidationError(string email)
    {
        Result<Property> result = Property.Create(id, tenantId, organizationId, auroraGrand with { ContactEmail = email }, now);

        Assert.Equal([("contactEmail", "property.contact_email_invalid")], FieldCodes(result));
    }

    [Fact]
    public void Create_WithTooLongName_ReturnsValidationError()
    {
        Result<Property> result = Property.Create(id, tenantId, organizationId, auroraGrand with { Name = new string('a', 201) }, now);

        Assert.Equal([("name", "property.name_too_long")], FieldCodes(result));
    }

    [Fact]
    public void Update_WithChangedDetails_ReplacesAndRaisesUpdated()
    {
        Property property = CreateProperty();
        PropertyDetails renamed = auroraGrand with { Name = "Aurora Grand Seattle" };

        Result result = property.Update(renamed, now.AddHours(1));

        Assert.True(result.IsSuccess);
        Assert.Equal(renamed, property.Details);
        Assert.Equal([new PropertyUpdated(id, renamed, now.AddHours(1))], property.DomainEvents);
    }

    [Fact]
    public void Update_WithSameDetailsAfterNormalizing_IsNoOp()
    {
        Property property = CreateProperty();

        Result result = property.Update(auroraGrand with { Name = " Aurora Grand " }, now.AddHours(1));

        Assert.True(result.IsSuccess);
        Assert.Empty(property.DomainEvents);
    }

    [Fact]
    public void Update_WithInvalidDetails_KeepsDetailsAndRaisesNothing()
    {
        Property property = CreateProperty();

        Result result = property.Update(auroraGrand with { Name = "" }, now.AddHours(1));

        Assert.Equal([("name", "property.name_required")], FieldCodes(result));
        Assert.Equal(auroraGrand, property.Details);
        Assert.Empty(property.DomainEvents);
    }

    private static Property CreateProperty()
    {
        Property property = Property.Create(id, tenantId, organizationId, auroraGrand, now).Value;
        property.DequeueDomainEvents();

        return property;
    }

    private static List<(string Field, string Code)> FieldCodes(Result result)
    {
        ValidationError error = Assert.IsType<ValidationError>(result.Error);

        return [.. error.Fields.Select(field => (field.Field, field.Code))];
    }
}
