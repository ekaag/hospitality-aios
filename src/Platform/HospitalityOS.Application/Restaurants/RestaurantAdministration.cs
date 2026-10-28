using HospitalityOS.Application.Administration;
using HospitalityOS.Application.Auditing;
using HospitalityOS.Contracts.Api.Restaurants;
using HospitalityOS.Domain.Dining.Restaurants;
using HospitalityOS.Ports.Bindings;
using HospitalityOS.Ports.Ids;
using HospitalityOS.Ports.Tenancy;
using HospitalityOS.SharedKernel.Ids;
using HospitalityOS.SharedKernel.Results;

namespace HospitalityOS.Application.Restaurants;

internal sealed class RestaurantAdministration(
    IRestaurantRepository restaurants,
    PropertyReferences references,
    ICurrentTenant currentTenant,
    AuditedCommit commit,
    IBindingResolver<IIdGenerator> idGenerators,
    TimeProvider timeProvider) : IRestaurantAdministration
{
    private const string LocationField = "locationSpaceId";
    private const string LocationNotFound = "restaurant.location_not_found";

    public async Task<Result<RestaurantResponse>> CreateAsync(CreateRestaurantRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        Result<PropertyId> property = await references.PropertyAsync(request.PropertyId, "restaurant", cancellationToken).ConfigureAwait(false);

        if (property.IsFailure)
        {
            return await commit.RejectAsync(AdministrationActions.RestaurantCreate, property.Error, null, cancellationToken).ConfigureAwait(false);
        }

        RequestFields fields = new();
        SpaceId? location = await references.LocationAsync(property.Value, request.LocationSpaceId, LocationField, LocationNotFound, fields, cancellationToken).ConfigureAwait(false);
        RestaurantDetails details = new(request.Name!, request.Cuisine, request.Description, location);
        RestaurantId id = new(idGenerators.Resolve().NewId());
        Result<Restaurant> created = fields.Merge(Restaurant.Create(id, currentTenant.TenantId, property.Value, details, timeProvider.GetUtcNow()));

        return await commit.CreateAsync(
            AdministrationActions.RestaurantCreate,
            created,
            restaurant => restaurants.AddAsync(restaurant, cancellationToken),
            restaurant => Target(restaurant.Id.Value, restaurant.PropertyId),
            Response,
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<Result<RestaurantResponse>> UpdateAsync(Guid id, UpdateRestaurantRequest request, long? expectedVersion, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        Restaurant? restaurant = await FindAsync(id, cancellationToken).ConfigureAwait(false);

        return await commit.ChangeAsync(
            AdministrationActions.RestaurantUpdate,
            Target(id, restaurant?.PropertyId),
            restaurant,
            AdministrationErrors.RestaurantNotFound,
            expectedVersion,
            async found =>
            {
                RequestFields fields = new();
                SpaceId? location = await references.LocationAsync(found.PropertyId, request.LocationSpaceId, LocationField, LocationNotFound, fields, cancellationToken).ConfigureAwait(false);
                RestaurantDetails details = new(request.Name!, request.Cuisine, request.Description, location);

                return fields.Apply(() => found.Update(details, timeProvider.GetUtcNow()));
            },
            Response,
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<Result<RestaurantResponse>> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        Restaurant? restaurant = await FindAsync(id, cancellationToken).ConfigureAwait(false);

        return restaurant is null ? AdministrationErrors.RestaurantNotFound : Response(restaurant);
    }

    public async Task<IReadOnlyList<RestaurantResponse>> ListAsync(PropertyId propertyId, CancellationToken cancellationToken)
    {
        IReadOnlyList<Restaurant> found = await restaurants.ListForPropertyAsync(propertyId, cancellationToken).ConfigureAwait(false);

        return [.. found.OrderBy(restaurant => restaurant.Details.Name, StringComparer.OrdinalIgnoreCase).Select(Response)];
    }

    // An empty GUID cannot be a restaurant ID, so it names no restaurant.
    private Task<Restaurant?> FindAsync(Guid id, CancellationToken cancellationToken) =>
        id == Guid.Empty ? Task.FromResult<Restaurant?>(null) : restaurants.GetByIdAsync(new RestaurantId(id), cancellationToken);

    private static AuditTarget Target(Guid id, PropertyId? propertyId) => new(nameof(Restaurant), id.ToString(), propertyId);

    private static RestaurantResponse Response(Restaurant restaurant)
    {
        RestaurantDetails details = restaurant.Details;

        return new RestaurantResponse(
            restaurant.Id.Value,
            restaurant.PropertyId,
            details.Name,
            details.Cuisine,
            details.Description,
            details.LocationSpaceId,
            restaurant.Version);
    }
}
