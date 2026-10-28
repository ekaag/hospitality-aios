using HospitalityOS.Api.Authentication;
using HospitalityOS.Api.Http;
using HospitalityOS.Application.Administration;
using HospitalityOS.Application.Amenities;
using HospitalityOS.Contracts.Api.Amenities;
using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Api.Amenities;

/// <summary>
/// <c>/api/v1/amenities</c>: the amenities of the caller's tenant's properties (Journey 2). Staff read; administrators
/// create and update.
/// </summary>
internal static class AmenityEndpoints
{
    public const string Route = "/api/v1/amenities";

    public static IEndpointRouteBuilder MapAmenityEndpoints(this IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder group = endpoints.MapGroup(Route).RequireAuthorization(StaffPolicies.Staff);

        group.MapPost("/", CreateAsync).WithName(AdministrationActions.AmenityCreate).RequireAuthorization(StaffPolicies.Admin);
        group.MapGet("/", ListAsync).WithName("Amenity.List");
        group.MapGet("/{id}", GetAsync).WithName("Amenity.Get");
        group.MapPut("/{id}", UpdateAsync).WithName(AdministrationActions.AmenityUpdate).RequireAuthorization(StaffPolicies.Admin);

        return endpoints;
    }

    private static async Task<IResult> CreateAsync(CreateAmenityRequest request, IAmenityAdministration administration, CancellationToken cancellationToken) =>
        VersionedResult.Created(await administration.CreateAsync(request, cancellationToken), amenity => $"{Route}/{amenity.Id}", amenity => amenity.Version);

    /// <summary>
    /// One property's amenities (<c>?propertyId=</c>, required).
    /// </summary>
    private static async Task<IResult> ListAsync(RecordId propertyId, IAmenityAdministration administration, CancellationToken cancellationToken) =>
        TypedResults.Ok(await administration.ListAsync(new PropertyId(propertyId.Value), cancellationToken));

    private static async Task<IResult> GetAsync(RecordId id, IAmenityAdministration administration, CancellationToken cancellationToken) =>
        VersionedResult.Ok(await administration.GetAsync(id.Value, cancellationToken), amenity => amenity.Version);

    private static Task<IResult> UpdateAsync(RecordId id, UpdateAmenityRequest request, HttpRequest http, IAmenityAdministration administration, CancellationToken cancellationToken) =>
        VersionedResult.ChangeAsync(
            http,
            expectedVersion => administration.UpdateAsync(id.Value, request, expectedVersion, cancellationToken),
            amenity => amenity.Version);
}
