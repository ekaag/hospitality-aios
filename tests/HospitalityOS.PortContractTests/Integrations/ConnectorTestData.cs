using HospitalityOS.IntegrationContracts;
using HospitalityOS.IntegrationContracts.Pms;
using HospitalityOS.IntegrationContracts.Pos;
using HospitalityOS.SharedKernel.Collections;
using HospitalityOS.SharedKernel.Ids;
using HospitalityOS.SharedKernel.Pricing;
using HospitalityOS.SharedKernel.Tenancy;

namespace HospitalityOS.PortContractTests.Integrations;

/// <summary>
/// Records the connector contract suites arrange in a provider's backend.
/// </summary>
internal static class ConnectorTestData
{
    public static TenantId Tenant { get; } = new(Guid.Parse("01890a5d-ac96-774b-bcce-c00000000001"));

    public static PropertyId AuroraGrand { get; } = new(Guid.Parse("01890a5d-ac96-774b-bcce-c00000000002"));

    public static PropertyId Harbourside { get; } = new(Guid.Parse("01890a5d-ac96-774b-bcce-c00000000003"));

    public static PmsStay Stay(string id, StayStatus status, string? unit = "412") =>
        new(new ExternalId(id), new PmsGuest(new ExternalId("G-" + id), "Alex", "en"), new DateOnly(2026, 10, 24), new DateOnly(2026, 10, 27), unit, status);

    public static PosMenu Menu(string id, string name, params PosMenuItem[] items) => new(new ExternalId(id), name, ValueList.From(items));

    public static PosMenuItem Item(string id, string name, decimal price, bool isAvailable = true) =>
        new(new ExternalId(id), name, null, "Mains", new Money(price, "GBP"), isAvailable);
}
