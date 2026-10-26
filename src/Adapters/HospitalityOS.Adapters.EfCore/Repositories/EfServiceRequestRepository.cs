using HospitalityOS.Adapters.EfCore.Persistence;
using HospitalityOS.Domain.Services.ServiceRequests;
using HospitalityOS.Ports.Tenancy;
using HospitalityOS.SharedKernel.Ids;
using Microsoft.EntityFrameworkCore;

namespace HospitalityOS.Adapters.EfCore.Repositories;

internal sealed class EfServiceRequestRepository(HospitalityDbContext context, EfUnitOfWork unitOfWork, ICurrentTenant currentTenant)
    : EfRepository<ServiceRequest, ServiceRequestId>(context, unitOfWork, currentTenant), IServiceRequestRepository
{
    private static readonly ServiceRequestStatus[] activeStatuses = [ServiceRequestStatus.Open, ServiceRequestStatus.InProgress, ServiceRequestStatus.Exception];

    public Task<ServiceRequest?> FindBySourceMessageAsync(GuestMessageId sourceMessageId, CancellationToken cancellationToken)
    {
        Guid key = sourceMessageId.Value;

        return FindFirstAsync(request => EF.Property<Guid?>(request, KeyColumns.SourceMessageId) == key, cancellationToken);
    }

    public Task<IReadOnlyList<ServiceRequest>> ListActiveForStayAsync(StayContextId stayContextId, CancellationToken cancellationToken)
    {
        // Mirrors ServiceRequest.IsActive, which is computed and so cannot be queried.
        return QueryAsync(
            requests => requests
                .Where(request => request.Origin.StayContextId == stayContextId && activeStatuses.Contains(request.Status))
                .OrderByDescending(request => request.CreatedAt)
                .ThenByDescending(request => request.Id),
            cancellationToken);
    }
}
