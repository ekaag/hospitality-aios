using HospitalityOS.Adapters.InMemory.Persistence;
using HospitalityOS.Domain.Services.ServiceRequests;
using HospitalityOS.Ports.Tenancy;
using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Adapters.InMemory.Repositories;

internal sealed class InMemoryServiceRequestRepository(InMemoryStore store, InMemoryUnitOfWork unitOfWork, ICurrentTenant currentTenant)
    : InMemoryRepository<ServiceRequest, ServiceRequestId>(store, unitOfWork, currentTenant), IServiceRequestRepository
{
    public Task<ServiceRequest?> FindBySourceMessageAsync(GuestMessageId sourceMessageId, CancellationToken cancellationToken)
    {
        return FindFirstAsync(request => request.Origin.SourceMessageId == sourceMessageId, cancellationToken);
    }

    public Task<IReadOnlyList<ServiceRequest>> ListActiveForStayAsync(StayContextId stayContextId, CancellationToken cancellationToken)
    {
        return QueryAsync(
            requests => requests
                .Where(request => request.Origin.StayContextId == stayContextId && request.IsActive)
                .OrderByDescending(request => request.CreatedAt)
                .ThenByDescending(request => request.Id.Value),
            cancellationToken);
    }
}
