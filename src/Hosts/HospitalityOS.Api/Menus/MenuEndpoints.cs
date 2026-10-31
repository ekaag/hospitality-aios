using HospitalityOS.Api.Authentication;
using HospitalityOS.Api.Http;
using HospitalityOS.Application.Administration;
using HospitalityOS.Application.Menus;
using HospitalityOS.Contracts.Api.Menus;

namespace HospitalityOS.Api.Menus;

/// <summary>
/// <c>/api/v1/menus</c>: the restaurants' menus (Journeys 3 and 7). Staff read; administrators create and replace menus
/// and mark items sold out.
/// </summary>
internal static class MenuEndpoints
{
    public const string Route = "/api/v1/menus";

    public static IEndpointRouteBuilder MapMenuEndpoints(this IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder group = endpoints.MapGroup(Route).RequireAuthorization(StaffPolicies.Staff);

        group.MapPost("/", CreateAsync).Produces<MenuResponse>(StatusCodes.Status201Created).WithName(AdministrationActions.MenuCreate).RequireAuthorization(StaffPolicies.Admin);
        group.MapGet("/", ListAsync).Produces<IReadOnlyList<MenuResponse>>().WithName("Menu.List");
        group.MapGet("/{id}", GetAsync).Produces<MenuResponse>().WithName("Menu.Get");
        group.MapPut("/{id}", UpdateAsync).Produces<MenuResponse>().WithName(AdministrationActions.MenuUpdate).RequireAuthorization(StaffPolicies.Admin);
        group.MapPut("/{id}/items/{itemId}/availability", SetItemAvailabilityAsync).Produces<MenuResponse>()
            .WithName(AdministrationActions.MenuSetItemAvailability)
            .RequireAuthorization(StaffPolicies.Admin);

        return endpoints;
    }

    private static async Task<IResult> CreateAsync(CreateMenuRequest request, IMenuAdministration administration, CancellationToken cancellationToken) =>
        VersionedResult.Created(await administration.CreateAsync(request, cancellationToken), menu => $"{Route}/{menu.Id}", menu => menu.Version);

    /// <summary>
    /// One restaurant's menus (<c>?restaurantId=</c>, required).
    /// </summary>
    private static async Task<IResult> ListAsync(RecordId restaurantId, IMenuAdministration administration, CancellationToken cancellationToken) =>
        TypedResults.Ok(await administration.ListAsync(restaurantId.Value, cancellationToken));

    private static async Task<IResult> GetAsync(RecordId id, IMenuAdministration administration, CancellationToken cancellationToken) =>
        VersionedResult.Ok(await administration.GetAsync(id.Value, cancellationToken), menu => menu.Version);

    private static Task<IResult> UpdateAsync(RecordId id, UpdateMenuRequest request, HttpRequest http, IMenuAdministration administration, CancellationToken cancellationToken) =>
        VersionedResult.ChangeAsync(
            http,
            expectedVersion => administration.UpdateAsync(id.Value, request, expectedVersion, cancellationToken),
            menu => menu.Version);

    private static Task<IResult> SetItemAvailabilityAsync(
        RecordId id,
        RecordId itemId,
        SetItemAvailabilityRequest request,
        HttpRequest http,
        IMenuAdministration administration,
        CancellationToken cancellationToken) =>
        VersionedResult.ChangeAsync(
            http,
            expectedVersion => administration.SetItemAvailabilityAsync(id.Value, itemId.Value, request, expectedVersion, cancellationToken),
            menu => menu.Version);
}
