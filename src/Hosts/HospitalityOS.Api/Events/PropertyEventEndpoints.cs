using HospitalityOS.Api.Authentication;
using HospitalityOS.Api.Http;
using HospitalityOS.Application.Administration;
using HospitalityOS.Application.Events;
using HospitalityOS.Contracts.Api.Events;
using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Api.Events;

/// <summary>
/// <c>/api/v1/events</c>: what is happening at the caller's tenant's properties (Journey 8). Staff read; administrators
/// schedule, edit, and cancel.
/// </summary>
internal static class PropertyEventEndpoints
{
    public const string Route = "/api/v1/events";

    public static IEndpointRouteBuilder MapPropertyEventEndpoints(this IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder group = endpoints.MapGroup(Route).RequireAuthorization(StaffPolicies.Staff);

        group.MapPost("/", CreateAsync).Produces<PropertyEventResponse>(StatusCodes.Status201Created).WithName(AdministrationActions.PropertyEventCreate).RequireAuthorization(StaffPolicies.Admin);
        group.MapGet("/", ListAsync).Produces<IReadOnlyList<PropertyEventResponse>>().WithName("PropertyEvent.List");
        group.MapGet("/{id}", GetAsync).Produces<PropertyEventResponse>().WithName("PropertyEvent.Get");
        group.MapPut("/{id}", UpdateAsync).Produces<PropertyEventResponse>().WithName(AdministrationActions.PropertyEventUpdate).RequireAuthorization(StaffPolicies.Admin);
        group.MapPost("/{id}/cancel", CancelAsync).Produces<PropertyEventResponse>().WithName(AdministrationActions.PropertyEventCancel).RequireAuthorization(StaffPolicies.Admin);

        return endpoints;
    }

    private static async Task<IResult> CreateAsync(CreatePropertyEventRequest request, IPropertyEventAdministration administration, CancellationToken cancellationToken) =>
        VersionedResult.Created(await administration.CreateAsync(request, cancellationToken), propertyEvent => $"{Route}/{propertyEvent.Id}", propertyEvent => propertyEvent.Version);

    /// <summary>
    /// One property's events, past and cancelled ones included (<c>?propertyId=</c>, required).
    /// </summary>
    private static async Task<IResult> ListAsync(RecordId propertyId, IPropertyEventAdministration administration, CancellationToken cancellationToken) =>
        TypedResults.Ok(await administration.ListAsync(new PropertyId(propertyId.Value), cancellationToken));

    private static async Task<IResult> GetAsync(RecordId id, IPropertyEventAdministration administration, CancellationToken cancellationToken) =>
        VersionedResult.Ok(await administration.GetAsync(id.Value, cancellationToken), propertyEvent => propertyEvent.Version);

    private static Task<IResult> UpdateAsync(RecordId id, UpdatePropertyEventRequest request, HttpRequest http, IPropertyEventAdministration administration, CancellationToken cancellationToken) =>
        VersionedResult.ChangeAsync(
            http,
            expectedVersion => administration.UpdateAsync(id.Value, request, expectedVersion, cancellationToken),
            propertyEvent => propertyEvent.Version);

    private static Task<IResult> CancelAsync(RecordId id, HttpRequest http, IPropertyEventAdministration administration, CancellationToken cancellationToken) =>
        VersionedResult.ChangeAsync(http, expectedVersion => administration.CancelAsync(id.Value, expectedVersion, cancellationToken), propertyEvent => propertyEvent.Version);
}
