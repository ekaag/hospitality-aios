using HospitalityOS.Contracts.Api.Events;
using HospitalityOS.SharedKernel.Ids;
using HospitalityOS.SharedKernel.Results;

namespace HospitalityOS.Application.Events;

/// <summary>
/// Schedules, edits, cancels, and reads the events of the caller's tenant's properties (Journey 8). Commands commit their
/// change and its audit record together; rejected commands are audited too. Scoped; the caller's tenant and actor come
/// from the operation.
/// </summary>
public interface IPropertyEventAdministration
{
    /// <summary>
    /// Schedules an event at a property and raises <c>PropertyEventCreated</c>.
    /// </summary>
    /// <returns>The event, or a <see cref="ValidationError"/> listing every invalid field (a property not in the tenant
    /// is the field <c>propertyId</c>; a location not in the property is <c>locationSpaceId</c>; a time given with an
    /// offset is <c>property_event.start_not_local</c> or <c>property_event.end_not_local</c>).</returns>
    Task<Result<PropertyEventResponse>> CreateAsync(CreatePropertyEventRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Replaces a scheduled event's information and raises <c>PropertyEventUpdated</c>; unchanged information is a no-op.
    /// </summary>
    /// <param name="id">The event.</param>
    /// <param name="request">The new information.</param>
    /// <param name="expectedVersion">The version the caller read, when it wants the change refused if someone changed the
    /// event since.</param>
    /// <param name="cancellationToken">Cancels the command.</param>
    /// <returns>The event; <c>property_event.not_found</c>; <c>request.version_mismatch</c>;
    /// <c>property_event.cancelled</c>; or a <see cref="ValidationError"/>.</returns>
    Task<Result<PropertyEventResponse>> UpdateAsync(Guid id, UpdatePropertyEventRequest request, long? expectedVersion, CancellationToken cancellationToken);

    /// <summary>
    /// Calls an event off for good and raises <c>PropertyEventUpdated</c>; guests are no longer offered it. Cancelling
    /// twice is a no-op.
    /// </summary>
    /// <returns>The event; <c>property_event.not_found</c>; or <c>request.version_mismatch</c>.</returns>
    Task<Result<PropertyEventResponse>> CancelAsync(Guid id, long? expectedVersion, CancellationToken cancellationToken);

    /// <summary>
    /// Reads an event.
    /// </summary>
    /// <returns>The event, or <c>property_event.not_found</c>.</returns>
    Task<Result<PropertyEventResponse>> GetAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Every event of a property, past and cancelled ones included, earliest first.
    /// </summary>
    Task<IReadOnlyList<PropertyEventResponse>> ListAsync(PropertyId propertyId, CancellationToken cancellationToken);
}
