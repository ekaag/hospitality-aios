using HospitalityOS.Api.Authentication;
using HospitalityOS.Api.Http;
using HospitalityOS.Application.Administration;
using HospitalityOS.Application.Spaces;
using HospitalityOS.Contracts.Api.Spaces;
using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Api.Spaces;

/// <summary>
/// <c>/api/v1/spaces</c>: the places of the caller's tenant's properties. Staff read; administrators create and update.
/// </summary>
internal static class SpaceEndpoints
{
    public const string Route = "/api/v1/spaces";

    public static IEndpointRouteBuilder MapSpaceEndpoints(this IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder group = endpoints.MapGroup(Route).RequireAuthorization(StaffPolicies.Staff);

        group.MapPost("/", CreateAsync).Produces<SpaceResponse>(StatusCodes.Status201Created).WithName(AdministrationActions.SpaceCreate).RequireAuthorization(StaffPolicies.Admin);
        group.MapGet("/", ListAsync).Produces<IReadOnlyList<SpaceResponse>>().WithName("Space.List");
        group.MapGet("/{id}", GetAsync).Produces<SpaceResponse>().WithName("Space.Get");
        group.MapPut("/{id}", UpdateAsync).Produces<SpaceResponse>().WithName(AdministrationActions.SpaceUpdate).RequireAuthorization(StaffPolicies.Admin);

        return endpoints;
    }

    private static async Task<IResult> CreateAsync(CreateSpaceRequest request, ISpaceAdministration administration, CancellationToken cancellationToken) =>
        VersionedResult.Created(await administration.CreateAsync(request, cancellationToken), space => $"{Route}/{space.Id}", space => space.Version);

    /// <summary>
    /// One property's spaces (<c>?propertyId=</c>, required).
    /// </summary>
    private static async Task<IResult> ListAsync(RecordId propertyId, ISpaceAdministration administration, CancellationToken cancellationToken) =>
        TypedResults.Ok(await administration.ListAsync(new PropertyId(propertyId.Value), cancellationToken));

    private static async Task<IResult> GetAsync(RecordId id, ISpaceAdministration administration, CancellationToken cancellationToken) =>
        VersionedResult.Ok(await administration.GetAsync(new SpaceId(id.Value), cancellationToken), space => space.Version);

    private static Task<IResult> UpdateAsync(RecordId id, UpdateSpaceRequest request, HttpRequest http, ISpaceAdministration administration, CancellationToken cancellationToken) =>
        VersionedResult.ChangeAsync(
            http,
            expectedVersion => administration.UpdateAsync(new SpaceId(id.Value), request, expectedVersion, cancellationToken),
            space => space.Version);
}
