using HospitalityOS.Contracts.Api.Restaurants;
using HospitalityOS.SharedKernel.Ids;
using HospitalityOS.SharedKernel.Results;

namespace HospitalityOS.Application.Restaurants;

/// <summary>
/// Creates, updates, and reads the restaurants of the caller's tenant's properties (Journey 3). Commands commit their
/// change and its audit record together; rejected commands are audited too. Scoped; the caller's tenant and actor come
/// from the operation.
/// </summary>
public interface IRestaurantAdministration
{
    /// <summary>
    /// Creates a restaurant for a property and raises <c>RestaurantCreated</c>.
    /// </summary>
    /// <returns>The restaurant, or a <see cref="ValidationError"/> listing every invalid field (a property not in the
    /// tenant is the field <c>propertyId</c>; a location not in the property is <c>locationSpaceId</c>).</returns>
    Task<Result<RestaurantResponse>> CreateAsync(CreateRestaurantRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Replaces a restaurant's information and raises <c>RestaurantUpdated</c>; unchanged information is a no-op.
    /// </summary>
    /// <param name="id">The restaurant.</param>
    /// <param name="request">The new information.</param>
    /// <param name="expectedVersion">The version the caller read, when it wants the change refused if someone changed the
    /// restaurant since.</param>
    /// <param name="cancellationToken">Cancels the command.</param>
    /// <returns>The restaurant; <c>restaurant.not_found</c>; <c>request.version_mismatch</c>; or a
    /// <see cref="ValidationError"/>.</returns>
    Task<Result<RestaurantResponse>> UpdateAsync(Guid id, UpdateRestaurantRequest request, long? expectedVersion, CancellationToken cancellationToken);

    /// <summary>
    /// Reads a restaurant.
    /// </summary>
    /// <returns>The restaurant, or <c>restaurant.not_found</c>.</returns>
    Task<Result<RestaurantResponse>> GetAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// A property's restaurants, by name.
    /// </summary>
    Task<IReadOnlyList<RestaurantResponse>> ListAsync(PropertyId propertyId, CancellationToken cancellationToken);
}
