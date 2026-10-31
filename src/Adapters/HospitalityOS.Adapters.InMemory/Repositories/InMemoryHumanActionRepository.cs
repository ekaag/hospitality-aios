using HospitalityOS.Adapters.InMemory.Persistence;
using HospitalityOS.Domain.Operations.HumanActions;
using HospitalityOS.Ports.Tenancy;
using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Adapters.InMemory.Repositories;

internal sealed class InMemoryHumanActionRepository(InMemoryStore store, InMemoryUnitOfWork unitOfWork, ICurrentTenant currentTenant)
    : InMemoryRepository<HumanAction, HumanActionId>(store, unitOfWork, currentTenant), IHumanActionRepository
{
    public Task<IReadOnlyList<HumanAction>> ListForServiceRequestAsync(ServiceRequestId serviceRequestId, CancellationToken cancellationToken)
    {
        return QueryAsync(
            actions => actions
                .Where(action => action.Details.ServiceRequestId == serviceRequestId)
                .OrderBy(action => action.CreatedAt)
                .ThenBy(action => action.Id.Value),
            cancellationToken);
    }

    public Task<IReadOnlyList<HumanAction>> ListOpenForPropertyAsync(PropertyId propertyId, CancellationToken cancellationToken)
    {
        return QueryAsync(
            actions => actions
                .Where(action => action.PropertyId == propertyId
                    && (action.Status == HumanActionStatus.Requested || action.Status == HumanActionStatus.Assigned
                        || action.Status == HumanActionStatus.Accepted || action.Status == HumanActionStatus.InProgress))
                .OrderBy(action => action.CreatedAt)
                .ThenBy(action => action.Id.Value),
            cancellationToken);
    }
}
