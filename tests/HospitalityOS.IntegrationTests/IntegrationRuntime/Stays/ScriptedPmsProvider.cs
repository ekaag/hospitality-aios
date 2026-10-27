using HospitalityOS.IntegrationContracts;
using HospitalityOS.IntegrationContracts.Pms;
using HospitalityOS.IntegrationContracts.Providers;
using HospitalityOS.SharedKernel.Results;

namespace HospitalityOS.IntegrationTests.IntegrationRuntime.Stays;

/// <summary>
/// A PMS whose stays a test changes between imports, standing in for a vendor PMS. Serves every property the same
/// stays.
/// </summary>
internal sealed class ScriptedPmsProvider(string name) : IIntegrationProvider<IPmsConnector>
{
    private volatile IReadOnlyList<PmsStay> stays = [];

    public string Name => name;

    public IReadOnlyList<PmsStay> Stays
    {
        get => stays;
        set => stays = value;
    }

    public IPmsConnector Create(ConnectorContext context) => new Connector(this);

    private sealed class Connector(ScriptedPmsProvider provider) : IPmsConnector
    {
        public Task<Result<PmsStay>> GetStayAsync(ExternalId stayId, CancellationToken cancellationToken)
        {
            PmsStay? stay = provider.Stays.FirstOrDefault(candidate => candidate.Id == stayId);

            return Task.FromResult<Result<PmsStay>>(stay is null ? IntegrationErrors.NotFound("stay", stayId) : stay);
        }

        public Task<IReadOnlyList<PmsStay>> ListInHouseStaysAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<PmsStay>>([.. provider.Stays.Where(stay => stay.Status == StayStatus.InHouse)]);
    }
}
