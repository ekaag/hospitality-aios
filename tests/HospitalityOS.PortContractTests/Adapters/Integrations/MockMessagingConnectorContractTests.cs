using HospitalityOS.IntegrationContracts.Messaging;
using HospitalityOS.IntegrationContracts.Providers;
using HospitalityOS.Integrations.Mock.Messaging;
using HospitalityOS.PortContractTests.Integrations;
using HospitalityOS.SharedKernel.Ids;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace HospitalityOS.PortContractTests.Adapters.Integrations;

public sealed class MockMessagingConnectorContractTests : MessagingConnectorContractTests
{
    private readonly MockMessagingGateway gateway;
    private readonly MockMessagingProvider provider;

    public MockMessagingConnectorContractTests()
    {
        MockMessagingOptions options = new();
        options.UnreachableAddresses.Add(UnreachableRecipient.Address);
        gateway = new MockMessagingGateway(Options.Create(options), new FakeTimeProvider(new DateTimeOffset(2026, 10, 27, 9, 0, 0, TimeSpan.Zero)));
        provider = new MockMessagingProvider(gateway);
    }

    protected override MessageRecipient UnreachableRecipient { get; } = new(MessageChannel.Sms, "+44 7700 900000");

    protected override IMessagingConnector CreateConnector(PropertyId propertyId) =>
        provider.Create(new ConnectorContext(ConnectorTestData.Tenant, propertyId, new Dictionary<string, string>()));

    protected override IReadOnlyList<OutboundMessage> Delivered(PropertyId propertyId) =>
        [.. gateway.DeliveredFor(propertyId).Select(delivered => delivered.Message)];
}
