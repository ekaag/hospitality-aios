using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Domain.Operations.HumanActions;

/// <summary>
/// Persistence port for <see cref="HumanAction"/>. Implementations are scoped to the current tenant (P10) and
/// commit through the unit of work. Staff views (My/Unassigned/Completed/Exceptions) go through read-side queries.
/// </summary>
public interface IHumanActionRepository
{
    /// <summary>
    /// Loads an action, or returns <c>null</c> when it does not exist in the current tenant.
    /// </summary>
    Task<HumanAction?> GetByIdAsync(HumanActionId id, CancellationToken cancellationToken);

    /// <summary>
    /// The actions fulfilling a service request, oldest first (Journey 12 status; I4 continuation).
    /// </summary>
    Task<IReadOnlyList<HumanAction>> ListForServiceRequestAsync(ServiceRequestId serviceRequestId, CancellationToken cancellationToken);

    /// <summary>
    /// Adds a new action, persisted when the unit of work commits.
    /// </summary>
    Task AddAsync(HumanAction humanAction, CancellationToken cancellationToken);
}
