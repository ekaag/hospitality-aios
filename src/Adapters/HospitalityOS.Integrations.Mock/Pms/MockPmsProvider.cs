using HospitalityOS.IntegrationContracts.Pms;
using HospitalityOS.IntegrationContracts.Providers;
using Microsoft.Extensions.Options;

namespace HospitalityOS.Integrations.Mock.Pms;

internal sealed class MockPmsProvider(IOptions<MockPmsOptions> options) : IIntegrationProvider<IPmsConnector>
{
    public const string ProviderName = "MockPms";

    public string Name => ProviderName;

    public IPmsConnector Create(ConnectorContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return new MockPmsConnector([.. options.Value.Stays.GetValueOrDefault(context.PropertyId) ?? []]);
    }
}
