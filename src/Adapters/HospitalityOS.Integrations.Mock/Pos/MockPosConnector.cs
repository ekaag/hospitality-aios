using HospitalityOS.IntegrationContracts;
using HospitalityOS.IntegrationContracts.Pos;
using HospitalityOS.SharedKernel.Results;

namespace HospitalityOS.Integrations.Mock.Pos;

/// <summary>
/// One property's mock POS: fixed outlets and their menus.
/// </summary>
internal sealed class MockPosConnector(IReadOnlyList<PosOutlet> outlets, IReadOnlyDictionary<ExternalId, IReadOnlyList<PosMenu>> menus) : IPosConnector
{
    public Task<IReadOnlyList<PosOutlet>> ListOutletsAsync(CancellationToken cancellationToken) => Task.FromResult(outlets);

    public Task<Result<IReadOnlyList<PosMenu>>> ListMenusAsync(ExternalId outletId, CancellationToken cancellationToken) =>
        Task.FromResult(menus.TryGetValue(outletId, out IReadOnlyList<PosMenu>? found)
            ? Result<IReadOnlyList<PosMenu>>.Success(found)
            : IntegrationErrors.NotFound("outlet", outletId));
}
