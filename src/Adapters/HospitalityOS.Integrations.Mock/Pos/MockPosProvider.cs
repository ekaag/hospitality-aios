using HospitalityOS.IntegrationContracts.Pos;
using HospitalityOS.IntegrationContracts.Providers;
using Microsoft.Extensions.Options;

namespace HospitalityOS.Integrations.Mock.Pos;

internal sealed class MockPosProvider(IOptions<MockPosOptions> options) : IIntegrationProvider<IPosConnector>
{
    public const string ProviderName = "MockPos";

    public string Name => ProviderName;

    public IPosConnector Create(ConnectorContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        List<PosOutlet> outlets = [.. options.Value.Outlets.GetValueOrDefault(context.PropertyId) ?? []];

        return new MockPosConnector(
            outlets,
            outlets.ToDictionary(outlet => outlet.Id, outlet => (IReadOnlyList<PosMenu>)[.. options.Value.Menus.GetValueOrDefault(outlet.Id) ?? []]));
    }
}
