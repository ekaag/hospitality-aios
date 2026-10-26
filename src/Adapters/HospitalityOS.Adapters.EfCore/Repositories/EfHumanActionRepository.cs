using HospitalityOS.Adapters.EfCore.Persistence;
using HospitalityOS.Domain.Operations.HumanActions;
using HospitalityOS.Ports.Tenancy;
using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Adapters.EfCore.Repositories;

internal sealed class EfHumanActionRepository(HospitalityDbContext context, EfUnitOfWork unitOfWork, ICurrentTenant currentTenant)
    : EfRepository<HumanAction, HumanActionId>(context, unitOfWork, currentTenant), IHumanActionRepository
{
    public Task<IReadOnlyList<HumanAction>> ListForServiceRequestAsync(ServiceRequestId serviceRequestId, CancellationToken cancellationToken)
    {
        return QueryAsync(
            actions => actions
                .Where(action => action.Details.ServiceRequestId == serviceRequestId)
                .OrderBy(action => action.CreatedAt)
                .ThenBy(action => action.Id),
            cancellationToken);
    }
}
