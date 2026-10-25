using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Domain.Services.ServiceRequests;

/// <summary>
/// Persistence port for <see cref="ServiceRequest"/>. Implementations are scoped to the current tenant (P10)
/// and commit through the unit of work.
/// </summary>
public interface IServiceRequestRepository
{
    /// <summary>
    /// Loads a request, or returns <c>null</c> when it does not exist in the current tenant.
    /// </summary>
    Task<ServiceRequest?> GetByIdAsync(ServiceRequestId id, CancellationToken cancellationToken);

    /// <summary>
    /// Finds the request created from a guest message, or returns <c>null</c>; checked before creating one so
    /// reprocessing a message cannot create a duplicate delivery (P8). Backed by a unique index in persistence.
    /// </summary>
    Task<ServiceRequest?> FindBySourceMessageAsync(GuestMessageId sourceMessageId, CancellationToken cancellationToken);

    /// <summary>
    /// The stay's unresolved requests, newest first (Journey 12: "where are my towels?").
    /// </summary>
    Task<IReadOnlyList<ServiceRequest>> ListActiveForStayAsync(StayContextId stayContextId, CancellationToken cancellationToken);

    /// <summary>
    /// Adds a new request, persisted when the unit of work commits.
    /// </summary>
    Task AddAsync(ServiceRequest serviceRequest, CancellationToken cancellationToken);
}
