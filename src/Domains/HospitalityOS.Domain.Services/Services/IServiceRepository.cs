using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Domain.Services.Services;

/// <summary>
/// Persistence port for <see cref="Service"/>. Implementations are scoped to the current tenant (P10) and
/// commit through the unit of work.
/// </summary>
public interface IServiceRepository
{
    /// <summary>
    /// Loads a service, or returns <c>null</c> when it does not exist in the current tenant.
    /// </summary>
    Task<Service?> GetByIdAsync(ServiceId id, CancellationToken cancellationToken);

    /// <summary>
    /// Finds the service with the given code in a property (case-insensitive), or returns <c>null</c>.
    /// Used to keep codes unique within a property.
    /// </summary>
    Task<Service?> FindByCodeAsync(PropertyId propertyId, string code, CancellationToken cancellationToken);

    /// <summary>
    /// Adds a new service, persisted when the unit of work commits.
    /// </summary>
    Task AddAsync(Service service, CancellationToken cancellationToken);
}
