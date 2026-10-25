namespace HospitalityOS.Domain.Properties.Amenities;

/// <summary>
/// Persistence port for <see cref="Amenity"/>. Implementations are scoped to the current tenant (P10) and
/// commit through the unit of work.
/// </summary>
public interface IAmenityRepository
{
    /// <summary>
    /// Loads an amenity, or returns <c>null</c> when it does not exist in the current tenant.
    /// </summary>
    Task<Amenity?> GetByIdAsync(AmenityId id, CancellationToken cancellationToken);

    /// <summary>
    /// Adds a new amenity, persisted when the unit of work commits.
    /// </summary>
    Task AddAsync(Amenity amenity, CancellationToken cancellationToken);
}
