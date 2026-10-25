using HospitalityOS.Domain.Organizations.Organizations;
using HospitalityOS.Domain.Organizations.Organizations.Events;
using HospitalityOS.SharedKernel.Ids;
using HospitalityOS.SharedKernel.Results;
using HospitalityOS.SharedKernel.Tenancy;
using Xunit;

namespace HospitalityOS.UnitTests.Domain.Organizations.Organizations;

public sealed class OrganizationTests
{
    private static readonly OrganizationId id = new(Guid.Parse("01890a5d-ac96-774b-bcce-b302099a8001"));
    private static readonly TenantId tenantId = new(Guid.Parse("01890a5d-ac96-774b-bcce-b302099a8002"));
    private static readonly DateTimeOffset now = new(2026, 10, 24, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_WithValidName_CreatesOrganizationAndRaisesCreated()
    {
        Result<Organization> result = Organization.Create(id, tenantId, "  Aurora Hotels  ", now);

        Organization organization = result.Value;
        Assert.Equal(id, organization.Id);
        Assert.Equal(tenantId, organization.TenantId);
        Assert.Equal("Aurora Hotels", organization.Name);
        Assert.Equal([new OrganizationCreated(id, "Aurora Hotels", now)], organization.DomainEvents);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Create_WithMissingName_ReturnsValidationError(string? name)
    {
        Result<Organization> result = Organization.Create(id, tenantId, name!, now);

        AssertSingleFieldError(result, "name", "organization.name_required");
    }

    [Fact]
    public void Create_WithTooLongName_ReturnsValidationError()
    {
        Result<Organization> result = Organization.Create(id, tenantId, new string('a', Organization.MaxNameLength + 1), now);

        AssertSingleFieldError(result, "name", "organization.name_too_long");
    }

    [Fact]
    public void Rename_WithNewName_UpdatesAndRaisesUpdated()
    {
        Organization organization = CreateOrganization();

        Result result = organization.Rename("Aurora Collection", now.AddHours(1));

        Assert.True(result.IsSuccess);
        Assert.Equal("Aurora Collection", organization.Name);
        Assert.Equal([new OrganizationUpdated(id, "Aurora Collection", now.AddHours(1))], organization.DomainEvents);
    }

    [Fact]
    public void Rename_WithSameName_IsNoOp()
    {
        Organization organization = CreateOrganization();

        Result result = organization.Rename(" Aurora Hotels ", now.AddHours(1));

        Assert.True(result.IsSuccess);
        Assert.Empty(organization.DomainEvents);
    }

    [Fact]
    public void Rename_WithInvalidName_KeepsNameAndRaisesNothing()
    {
        Organization organization = CreateOrganization();

        Result result = organization.Rename(" ", now.AddHours(1));

        AssertSingleFieldError(result, "name", "organization.name_required");
        Assert.Equal("Aurora Hotels", organization.Name);
        Assert.Empty(organization.DomainEvents);
    }

    private static Organization CreateOrganization()
    {
        Organization organization = Organization.Create(id, tenantId, "Aurora Hotels", now).Value;
        organization.DequeueDomainEvents();

        return organization;
    }

    private static void AssertSingleFieldError(Result result, string field, string code)
    {
        ValidationError error = Assert.IsType<ValidationError>(result.Error);
        FieldError fieldError = Assert.Single(error.Fields);
        Assert.Equal(field, fieldError.Field);
        Assert.Equal(code, fieldError.Code);
    }
}
