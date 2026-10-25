using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Domain.Properties.AccommodationUnits;

/// <summary>
/// Persistence port for <see cref="AccommodationUnit"/>. Implementations are scoped to the current tenant
/// (P10) and commit through the unit of work.
/// </summary>
public interface IAccommodationUnitRepository
{
    /// <summary>
    /// Loads a unit, or returns <c>null</c> when it does not exist in the current tenant.
    /// </summary>
    Task<AccommodationUnit?> GetByIdAsync(AccommodationUnitId id, CancellationToken cancellationToken);

    /// <summary>
    /// Finds the unit with the given number in a property (case-insensitive), or returns <c>null</c>.
    /// Used to keep unit numbers unique within a property.
    /// </summary>
    Task<AccommodationUnit?> FindByUnitNumberAsync(PropertyId propertyId, string unitNumber, CancellationToken cancellationToken);

    /// <summary>
    /// Adds a new unit, persisted when the unit of work commits.
    /// </summary>
    Task AddAsync(AccommodationUnit accommodationUnit, CancellationToken cancellationToken);
}
