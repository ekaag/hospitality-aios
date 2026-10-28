using HospitalityOS.Contracts.Api.Hours;
using HospitalityOS.SharedKernel.Ids;
using HospitalityOS.SharedKernel.Results;

namespace HospitalityOS.Application.Hours;

/// <summary>
/// Sets and reads when the property and its spaces, amenities, restaurants, and services are open (Journeys 2 and 16).
/// Commands commit their change and its audit record together; rejected commands are audited too. Scoped; the caller's
/// tenant and actor come from the operation.
/// </summary>
public interface IOperatingHoursAdministration
{
    /// <summary>
    /// Creates the operating hours of a subject in a property and raises <c>OperatingHoursCreated</c>.
    /// </summary>
    /// <returns>The hours; <c>hours.already_set</c> when the subject has hours; or a <see cref="ValidationError"/>
    /// listing every invalid field (a subject not in the property is <c>subject.id</c>).</returns>
    Task<Result<OperatingHoursResponse>> CreateAsync(CreateOperatingHoursRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Replaces the schedule and raises <c>OperatingHoursUpdated</c>; an unchanged schedule is a no-op.
    /// </summary>
    /// <param name="id">The operating hours record.</param>
    /// <param name="request">The new schedule.</param>
    /// <param name="expectedVersion">The version the caller read, when it wants the change refused if someone changed the
    /// hours since.</param>
    /// <param name="cancellationToken">Cancels the command.</param>
    /// <returns>The hours; <c>hours.not_found</c>; <c>request.version_mismatch</c>; or a <see cref="ValidationError"/>.</returns>
    Task<Result<OperatingHoursResponse>> UpdateAsync(Guid id, UpdateOperatingHoursRequest request, long? expectedVersion, CancellationToken cancellationToken);

    /// <summary>
    /// Reads an operating hours record.
    /// </summary>
    /// <returns>The hours, or <c>hours.not_found</c>.</returns>
    Task<Result<OperatingHoursResponse>> GetAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// A property's operating hours records, by subject kind.
    /// </summary>
    Task<IReadOnlyList<OperatingHoursResponse>> ListAsync(PropertyId propertyId, CancellationToken cancellationToken);
}
