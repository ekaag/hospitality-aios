namespace HospitalityOS.Domain.Properties.AccommodationTypes;

/// <summary>
/// Persistence port for <see cref="AccommodationType"/>. Implementations are scoped to the current tenant
/// (P10) and commit through the unit of work.
/// </summary>
public interface IAccommodationTypeRepository
{
    /// <summary>
    /// Loads an accommodation type, or returns <c>null</c> when it does not exist in the current tenant.
    /// </summary>
    Task<AccommodationType?> GetByIdAsync(AccommodationTypeId id, CancellationToken cancellationToken);

    /// <summary>
    /// Adds a new accommodation type, persisted when the unit of work commits.
    /// </summary>
    Task AddAsync(AccommodationType accommodationType, CancellationToken cancellationToken);
}
