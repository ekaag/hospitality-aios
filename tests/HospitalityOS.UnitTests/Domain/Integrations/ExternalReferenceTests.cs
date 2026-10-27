using HospitalityOS.Domain.Integrations.ExternalReferences;
using HospitalityOS.Domain.Integrations.ExternalReferences.Events;
using HospitalityOS.IntegrationContracts;
using HospitalityOS.SharedKernel.Ids;
using HospitalityOS.SharedKernel.Results;
using HospitalityOS.SharedKernel.Tenancy;
using Xunit;

namespace HospitalityOS.UnitTests.Domain.Integrations;

public sealed class ExternalReferenceTests
{
    private static readonly ExternalReferenceId id = new(Guid.Parse("01890a5d-ac96-774b-bcce-b30209907001"));
    private static readonly TenantId tenantId = new(Guid.Parse("01890a5d-ac96-774b-bcce-b30209907002"));
    private static readonly PropertyId auroraGrand = new(Guid.Parse("01890a5d-ac96-774b-bcce-b30209907003"));
    private static readonly Guid alexStay = Guid.Parse("01890a5d-ac96-774b-bcce-b30209907004");
    private static readonly DateTimeOffset now = new(2026, 10, 27, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_KeepsExternalIdExactlyAndRaisesCreated()
    {
        ExternalReference reference = ExternalReference.Create(id, tenantId, auroraGrand, "MockPms", "StayContext", " RES-0412 ", alexStay, now).Value;

        Assert.Equal((" RES-0412 ", alexStay), (reference.ExternalId, reference.CanonicalId));
        Assert.Equal([new ExternalReferenceCreated(id, auroraGrand, "MockPms", "StayContext", " RES-0412 ", alexStay, now)], reference.DomainEvents);
    }

    [Fact]
    public void Create_WithInvalidFields_ReportsAll()
    {
        Result<ExternalReference> result = ExternalReference.Create(
            id, tenantId, auroraGrand, "Mock Pms", "stayContext", new string('x', ExternalReference.MaxExternalIdLength + 1), Guid.Empty, now);

        Assert.Equal(
            [
                ("providerName", "integration.provider_invalid"),
                ("entityType", "integration.entity_type_invalid"),
                ("externalId", "integration.external_id_too_long"),
                ("canonicalId", "integration.canonical_id_required"),
            ],
            FieldCodes(result));
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void Create_WithBlankExternalId_IsInvalid(string externalId)
    {
        Result<ExternalReference> result = ExternalReference.Create(id, tenantId, auroraGrand, "MockPms", "StayContext", externalId, alexStay, now);

        Assert.Equal([("externalId", "integration.external_id_required")], FieldCodes(result));
    }

    [Fact]
    public void MaxExternalIdLength_MatchesTheContractsExternalId()
    {
        Assert.Equal(ExternalId.MaxLength, ExternalReference.MaxExternalIdLength);
    }

    private static List<(string Field, string Code)> FieldCodes(Result result)
    {
        ValidationError error = Assert.IsType<ValidationError>(result.Error);

        return [.. error.Fields.Select(field => (field.Field, field.Code))];
    }
}
