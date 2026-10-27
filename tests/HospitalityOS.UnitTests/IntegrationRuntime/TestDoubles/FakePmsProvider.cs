using HospitalityOS.IntegrationContracts;
using HospitalityOS.IntegrationContracts.Pms;
using HospitalityOS.IntegrationContracts.Providers;
using HospitalityOS.Ports.Bindings;
using HospitalityOS.SharedKernel.Results;

namespace HospitalityOS.UnitTests.IntegrationRuntime.TestDoubles;

/// <summary>
/// A PMS provider that requires a <c>hotelCode</c> setting, counts the connectors it creates, and whose connectors
/// report <paramref name="health"/> (healthy when omitted).
/// </summary>
internal sealed class FakePmsProvider(string name, Func<AdapterHealth>? health = null) : IIntegrationProvider<IPmsConnector>
{
    public string Name => name;

    public int Created { get; private set; }

    public IEnumerable<string> ValidateSettings(IReadOnlyDictionary<string, string> settings) =>
        settings.ContainsKey("hotelCode") ? [] : ["hotelCode is required."];

    public IPmsConnector Create(ConnectorContext context)
    {
        Created++;

        return new FakePmsConnector(name, context, health ?? (() => AdapterHealth.Healthy));
    }
}

internal sealed class FakePmsConnector(string providerName, ConnectorContext context, Func<AdapterHealth> health) : IPmsConnector, IAdapterHealthProbe
{
    public string ProviderName => providerName;

    public ConnectorContext Context => context;

    public Task<Result<PmsStay>> GetStayAsync(ExternalId stayId, CancellationToken cancellationToken) =>
        Task.FromResult<Result<PmsStay>>(IntegrationErrors.NotFound("stay", stayId));

    public Task<IReadOnlyList<PmsStay>> ListInHouseStaysAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<PmsStay>>([]);

    public Task<AdapterHealth> CheckHealthAsync(CancellationToken cancellationToken) => Task.FromResult(health());
}
