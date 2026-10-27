using HospitalityOS.IntegrationContracts;
using HospitalityOS.IntegrationContracts.Pms;
using HospitalityOS.SharedKernel.Ids;
using HospitalityOS.SharedKernel.Results;
using Xunit;
using static HospitalityOS.PortContractTests.Integrations.ConnectorTestData;

namespace HospitalityOS.PortContractTests.Integrations;

/// <summary>
/// What every <see cref="IPmsConnector"/> provider must do, so a property can swap PMS by configuration (Journey 18).
/// </summary>
public abstract class PmsConnectorContractTests
{
    protected static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    /// <summary>
    /// The provider's connector for <paramref name="propertyId"/>, with the provider's system holding <paramref name="stays"/>.
    /// </summary>
    protected abstract IPmsConnector CreateConnector(PropertyId propertyId, IReadOnlyDictionary<PropertyId, IReadOnlyList<PmsStay>> stays);

    [Fact]
    public async Task GetStay_Known_ReturnsItInCanonicalShape()
    {
        PmsStay stay = Stay("RES-0412", StayStatus.InHouse);
        IPmsConnector connector = CreateConnector(AuroraGrand, new Dictionary<PropertyId, IReadOnlyList<PmsStay>> { [AuroraGrand] = [stay] });

        Result<PmsStay> found = await connector.GetStayAsync(new ExternalId("RES-0412"), Cancellation);

        Assert.Equal(stay, found.Value);
    }

    [Fact]
    public async Task GetStay_UnknownOrAnotherPropertys_IsNotFound()
    {
        IPmsConnector connector = CreateConnector(AuroraGrand, new Dictionary<PropertyId, IReadOnlyList<PmsStay>>
        {
            [AuroraGrand] = [Stay("RES-0412", StayStatus.InHouse)],
            [Harbourside] = [Stay("RES-0900", StayStatus.InHouse)],
        });

        Result<PmsStay> unknown = await connector.GetStayAsync(new ExternalId("RES-9999"), Cancellation);
        Result<PmsStay> otherProperty = await connector.GetStayAsync(new ExternalId("RES-0900"), Cancellation);

        Assert.All([unknown, otherProperty], result => Assert.Equal((IntegrationErrors.NotFoundCode, ErrorKind.NotFound), (result.Error.Code, result.Error.Kind)));
    }

    [Fact]
    public async Task ListInHouseStays_ReturnsOnlyThePropertysInHouseStays()
    {
        IPmsConnector connector = CreateConnector(AuroraGrand, new Dictionary<PropertyId, IReadOnlyList<PmsStay>>
        {
            [AuroraGrand] =
            [
                Stay("RES-0412", StayStatus.InHouse),
                Stay("RES-0415", StayStatus.InHouse, "415"),
                Stay("RES-0500", StayStatus.Reserved, null),
                Stay("RES-0301", StayStatus.CheckedOut, "301"),
                Stay("RES-0302", StayStatus.Cancelled, null),
            ],
            [Harbourside] = [Stay("RES-0900", StayStatus.InHouse)],
        });

        IReadOnlyList<PmsStay> inHouse = await connector.ListInHouseStaysAsync(Cancellation);

        Assert.Equal(["RES-0412", "RES-0415"], inHouse.Select(stay => stay.Id.Value).Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task ListInHouseStays_WhenNobodyIsStaying_IsEmpty()
    {
        IPmsConnector connector = CreateConnector(AuroraGrand, new Dictionary<PropertyId, IReadOnlyList<PmsStay>>());

        Assert.Empty(await connector.ListInHouseStaysAsync(Cancellation));
    }
}
