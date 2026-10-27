using HospitalityOS.IntegrationContracts;
using HospitalityOS.IntegrationContracts.Pos;
using HospitalityOS.SharedKernel.Ids;
using HospitalityOS.SharedKernel.Results;
using Xunit;
using static HospitalityOS.PortContractTests.Integrations.ConnectorTestData;

namespace HospitalityOS.PortContractTests.Integrations;

/// <summary>
/// What every <see cref="IPosConnector"/> provider must do.
/// </summary>
public abstract class PosConnectorContractTests
{
    private static readonly PosOutlet saffron = new(new ExternalId("OUT-1"), "Saffron");
    private static readonly PosOutlet harbourBar = new(new ExternalId("OUT-9"), "Harbour Bar");
    private static readonly PosMenu dinner = Menu("MENU-1", "Dinner", Item("ITEM-1", "Paneer tikka", 14.50m), Item("ITEM-2", "Tarka dal", 11m, isAvailable: false));
    private static readonly PosMenu lunch = Menu("MENU-2", "Lunch", Item("ITEM-3", "Thali", 16m));

    protected static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    /// <summary>
    /// The provider's connector for <paramref name="propertyId"/>, with the provider's system holding the outlets and their menus.
    /// </summary>
    protected abstract IPosConnector CreateConnector(PropertyId propertyId, IReadOnlyDictionary<PropertyId, IReadOnlyList<(PosOutlet Outlet, IReadOnlyList<PosMenu> Menus)>> outlets);

    [Fact]
    public async Task ListOutlets_ReturnsOnlyThePropertysOutlets()
    {
        IPosConnector connector = CreateConnector(AuroraGrand, Outlets());

        IReadOnlyList<PosOutlet> outlets = await connector.ListOutletsAsync(Cancellation);

        Assert.Equal([saffron], outlets);
    }

    [Fact]
    public async Task ListMenus_ReturnsTheOutletsMenusWithPricesAndAvailability()
    {
        IPosConnector connector = CreateConnector(AuroraGrand, Outlets());

        Result<IReadOnlyList<PosMenu>> menus = await connector.ListMenusAsync(saffron.Id, Cancellation);

        Assert.Equal([dinner, lunch], menus.Value.OrderBy(menu => menu.Id.Value, StringComparer.Ordinal));
    }

    [Fact]
    public async Task ListMenus_OfAnUnknownOrAnotherPropertysOutlet_IsNotFound()
    {
        IPosConnector connector = CreateConnector(AuroraGrand, Outlets());

        Result<IReadOnlyList<PosMenu>> unknown = await connector.ListMenusAsync(new ExternalId("OUT-404"), Cancellation);
        Result<IReadOnlyList<PosMenu>> otherProperty = await connector.ListMenusAsync(harbourBar.Id, Cancellation);

        Assert.All([unknown, otherProperty], result => Assert.Equal((IntegrationErrors.NotFoundCode, ErrorKind.NotFound), (result.Error.Code, result.Error.Kind)));
    }

    private static Dictionary<PropertyId, IReadOnlyList<(PosOutlet Outlet, IReadOnlyList<PosMenu> Menus)>> Outlets() => new()
    {
        [AuroraGrand] = [(saffron, [dinner, lunch])],
        [Harbourside] = [(harbourBar, [Menu("MENU-9", "Drinks")])],
    };
}
