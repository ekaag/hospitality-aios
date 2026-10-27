using HospitalityOS.IntegrationContracts.Messaging;
using HospitalityOS.IntegrationContracts.Providers;

namespace HospitalityOS.Integrations.Mock.Messaging;

internal sealed class MockMessagingProvider(MockMessagingGateway gateway) : IIntegrationProvider<IMessagingConnector>
{
    public const string ProviderName = "MockMessaging";

    public string Name => ProviderName;

    public IMessagingConnector Create(ConnectorContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return new MockMessagingConnector(gateway, context.PropertyId);
    }
}
