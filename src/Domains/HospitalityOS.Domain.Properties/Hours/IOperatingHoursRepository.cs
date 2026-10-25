using HospitalityOS.Domain.Properties.Shared;
using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Domain.Properties.Hours;

/// <summary>
/// Persistence port for <see cref="OperatingHours"/>. Implementations are scoped to the current tenant (P10)
/// and commit through the unit of work.
/// </summary>
public interface IOperatingHoursRepository
{
    /// <summary>
    /// Loads operating hours, or returns <c>null</c> when they do not exist in the current tenant.
    /// </summary>
    Task<OperatingHours?> GetByIdAsync(OperatingHoursId id, CancellationToken cancellationToken);

    /// <summary>
    /// Finds the hours for a subject in a property, or returns <c>null</c>. Used to keep one record per subject.
    /// </summary>
    Task<OperatingHours?> FindBySubjectAsync(PropertyId propertyId, SubjectReference subject, CancellationToken cancellationToken);

    /// <summary>
    /// Adds new operating hours, persisted when the unit of work commits.
    /// </summary>
    Task AddAsync(OperatingHours operatingHours, CancellationToken cancellationToken);
}
