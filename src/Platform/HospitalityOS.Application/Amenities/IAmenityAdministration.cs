using HospitalityOS.Contracts.Api.Amenities;
using HospitalityOS.SharedKernel.Ids;
using HospitalityOS.SharedKernel.Results;

namespace HospitalityOS.Application.Amenities;

/// <summary>
/// Creates, updates, and reads the amenities of the caller's tenant's properties (Journey 2). Commands commit their
/// change and its audit record together; rejected commands are audited too. Scoped; the caller's tenant and actor come
/// from the operation.
/// </summary>
public interface IAmenityAdministration
{
    /// <summary>
    /// Creates an amenity for a property and raises <c>AmenityCreated</c>.
    /// </summary>
    /// <returns>The amenity, or a <see cref="ValidationError"/> listing every invalid field (a property not in the tenant
    /// is the field <c>propertyId</c>; a location not in the property is <c>locationSpaceId</c>).</returns>
    Task<Result<AmenityResponse>> CreateAsync(CreateAmenityRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Replaces an amenity's information and location and raises <c>AmenityUpdated</c>; unchanged information is a no-op.
    /// </summary>
    /// <param name="id">The amenity.</param>
    /// <param name="request">The new information.</param>
    /// <param name="expectedVersion">The version the caller read, when it wants the change refused if someone changed the
    /// amenity since.</param>
    /// <param name="cancellationToken">Cancels the command.</param>
    /// <returns>The amenity; <c>amenity.not_found</c>; <c>request.version_mismatch</c>; or a
    /// <see cref="ValidationError"/>.</returns>
    Task<Result<AmenityResponse>> UpdateAsync(Guid id, UpdateAmenityRequest request, long? expectedVersion, CancellationToken cancellationToken);

    /// <summary>
    /// Reads an amenity.
    /// </summary>
    /// <returns>The amenity, or <c>amenity.not_found</c>.</returns>
    Task<Result<AmenityResponse>> GetAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// A property's amenities, by name.
    /// </summary>
    Task<IReadOnlyList<AmenityResponse>> ListAsync(PropertyId propertyId, CancellationToken cancellationToken);
}
