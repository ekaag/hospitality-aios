using HospitalityOS.IntegrationContracts;
using HospitalityOS.IntegrationContracts.Messaging;
using HospitalityOS.IntegrationContracts.Pms;
using HospitalityOS.IntegrationContracts.Providers;
using HospitalityOS.Ports.Bindings;
using HospitalityOS.SharedKernel.Results;

namespace HospitalityOS.IntegrationTests.IntegrationRuntime;

/// <summary>
/// A PMS provider requiring a <c>hotelCode</c> setting; its connectors remember who created them and with what.
/// </summary>
internal sealed class TestPmsProvider(string name) : IIntegrationProvider<IPmsConnector>
{
    public string Name => name;

    public IEnumerable<string> ValidateSettings(IReadOnlyDictionary<string, string> settings) =>
        settings.ContainsKey("hotelCode") ? [] : ["hotelCode is required."];

    public IPmsConnector Create(ConnectorContext context) => new TestPmsConnector(name, context);
}

internal sealed class TestPmsConnector(string providerName, ConnectorContext context) : IPmsConnector
{
    public string ProviderName => providerName;

    public ConnectorContext Context => context;

    public Task<Result<PmsStay>> GetStayAsync(ExternalId stayId, CancellationToken cancellationToken) =>
        Task.FromResult<Result<PmsStay>>(IntegrationErrors.NotFound("stay", stayId));

    public Task<IReadOnlyList<PmsStay>> ListInHouseStaysAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<PmsStay>>([]);
}

/// <summary>
/// A messaging provider whose connectors report unhealthy when the <c>health</c> setting is <c>down</c>.
/// </summary>
internal sealed class TestMessagingProvider : IIntegrationProvider<IMessagingConnector>
{
    public string Name => "TestMessaging";

    public IMessagingConnector Create(ConnectorContext context) =>
        new TestMessagingConnector(context.Settings.GetValueOrDefault("health") == "down");
}

internal sealed class TestMessagingConnector(bool down) : IMessagingConnector, IAdapterHealthProbe
{
    public Task<Result<MessageReceipt>> SendAsync(OutboundMessage message, CancellationToken cancellationToken) =>
        Task.FromResult<Result<MessageReceipt>>(IntegrationErrors.Rejected("Test connector sends nothing."));

    public Task<AdapterHealth> CheckHealthAsync(CancellationToken cancellationToken) =>
        Task.FromResult(down ? AdapterHealth.Unhealthy("Messaging gateway down") : AdapterHealth.Healthy);
}
