using HospitalityOS.Api.Authentication;
using HospitalityOS.Api.Http;
using HospitalityOS.Application.Administration;
using HospitalityOS.Application.Restaurants;
using HospitalityOS.Contracts.Api.Restaurants;
using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Api.Restaurants;

/// <summary>
/// <c>/api/v1/restaurants</c>: the restaurants and bars of the caller's tenant's properties (Journey 3). Staff read;
/// administrators create and update.
/// </summary>
internal static class RestaurantEndpoints
{
    public const string Route = "/api/v1/restaurants";

    public static IEndpointRouteBuilder MapRestaurantEndpoints(this IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder group = endpoints.MapGroup(Route).RequireAuthorization(StaffPolicies.Staff);

        group.MapPost("/", CreateAsync).WithName(AdministrationActions.RestaurantCreate).RequireAuthorization(StaffPolicies.Admin);
        group.MapGet("/", ListAsync).WithName("Restaurant.List");
        group.MapGet("/{id}", GetAsync).WithName("Restaurant.Get");
        group.MapPut("/{id}", UpdateAsync).WithName(AdministrationActions.RestaurantUpdate).RequireAuthorization(StaffPolicies.Admin);

        return endpoints;
    }

    private static async Task<IResult> CreateAsync(CreateRestaurantRequest request, IRestaurantAdministration administration, CancellationToken cancellationToken) =>
        VersionedResult.Created(await administration.CreateAsync(request, cancellationToken), restaurant => $"{Route}/{restaurant.Id}", restaurant => restaurant.Version);

    /// <summary>
    /// One property's restaurants (<c>?propertyId=</c>, required).
    /// </summary>
    private static async Task<IResult> ListAsync(RecordId propertyId, IRestaurantAdministration administration, CancellationToken cancellationToken) =>
        TypedResults.Ok(await administration.ListAsync(new PropertyId(propertyId.Value), cancellationToken));

    private static async Task<IResult> GetAsync(RecordId id, IRestaurantAdministration administration, CancellationToken cancellationToken) =>
        VersionedResult.Ok(await administration.GetAsync(id.Value, cancellationToken), restaurant => restaurant.Version);

    private static Task<IResult> UpdateAsync(RecordId id, UpdateRestaurantRequest request, HttpRequest http, IRestaurantAdministration administration, CancellationToken cancellationToken) =>
        VersionedResult.ChangeAsync(
            http,
            expectedVersion => administration.UpdateAsync(id.Value, request, expectedVersion, cancellationToken),
            restaurant => restaurant.Version);
}
