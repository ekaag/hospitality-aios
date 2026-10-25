namespace HospitalityOS.Domain.Activities.PropertyEvents;

/// <summary>
/// Persistence port for <see cref="PropertyEvent"/>. Implementations are scoped to the current tenant (P10)
/// and commit through the unit of work. Discovery queries go through read-side queries (D3) that apply
/// <see cref="PropertyEvent.SelectUpcoming"/>'s rules.
/// </summary>
public interface IPropertyEventRepository
{
    /// <summary>
    /// Loads an event, or returns <c>null</c> when it does not exist in the current tenant.
    /// </summary>
    Task<PropertyEvent?> GetByIdAsync(PropertyEventId id, CancellationToken cancellationToken);

    /// <summary>
    /// Adds a new event, persisted when the unit of work commits.
    /// </summary>
    Task AddAsync(PropertyEvent propertyEvent, CancellationToken cancellationToken);
}
