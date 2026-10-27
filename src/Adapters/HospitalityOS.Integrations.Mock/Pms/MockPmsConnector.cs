using HospitalityOS.IntegrationContracts;
using HospitalityOS.IntegrationContracts.Pms;
using HospitalityOS.SharedKernel.Results;

namespace HospitalityOS.Integrations.Mock.Pms;

/// <summary>
/// One property's mock PMS: a fixed list of stays.
/// </summary>
internal sealed class MockPmsConnector(IReadOnlyList<PmsStay> stays) : IPmsConnector
{
    public Task<Result<PmsStay>> GetStayAsync(ExternalId stayId, CancellationToken cancellationToken)
    {
        PmsStay? stay = stays.FirstOrDefault(candidate => candidate.Id == stayId);

        return Task.FromResult<Result<PmsStay>>(stay is null ? IntegrationErrors.NotFound("stay", stayId) : stay);
    }

    public Task<IReadOnlyList<PmsStay>> ListInHouseStaysAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<PmsStay>>([.. stays.Where(stay => stay.Status == StayStatus.InHouse)]);
}
