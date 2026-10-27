using HospitalityOS.IntegrationContracts.Pos;
using HospitalityOS.IntegrationContracts.Providers;
using HospitalityOS.Integrations.Mock.Pos;
using HospitalityOS.PortContractTests.Integrations;
using HospitalityOS.SharedKernel.Ids;
using Microsoft.Extensions.Options;

namespace HospitalityOS.PortContractTests.Adapters.Integrations;

public sealed class MockPosConnectorContractTests : PosConnectorContractTests
{
    protected override IPosConnector CreateConnector(PropertyId propertyId, IReadOnlyDictionary<PropertyId, IReadOnlyList<(PosOutlet Outlet, IReadOnlyList<PosMenu> Menus)>> outlets)
    {
        MockPosOptions options = new();

        foreach ((PropertyId property, IReadOnlyList<(PosOutlet Outlet, IReadOnlyList<PosMenu> Menus)> propertyOutlets) in outlets)
        {
            options.Outlets[property] = [.. propertyOutlets.Select(entry => entry.Outlet)];

            foreach ((PosOutlet outlet, IReadOnlyList<PosMenu> menus) in propertyOutlets)
            {
                options.Menus[outlet.Id] = [.. menus];
            }
        }

        return new MockPosProvider(Options.Create(options)).Create(new ConnectorContext(ConnectorTestData.Tenant, propertyId, new Dictionary<string, string>()));
    }
}
