using HospitalityOS.Api.Authentication;
using HospitalityOS.Api.Http;
using HospitalityOS.Application.Administration;
using HospitalityOS.Application.Hours;
using HospitalityOS.Contracts.Api.Hours;
using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Api.Hours;

/// <summary>
/// <c>/api/v1/operating-hours</c>: when the property and its spaces, amenities, restaurants, and services are open
/// (Journeys 2 and 16). Staff read; administrators create and update.
/// </summary>
internal static class OperatingHoursEndpoints
{
    public const string Route = "/api/v1/operating-hours";

    public static IEndpointRouteBuilder MapOperatingHoursEndpoints(this IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder group = endpoints.MapGroup(Route).RequireAuthorization(StaffPolicies.Staff);

        group.MapPost("/", CreateAsync).WithName(AdministrationActions.OperatingHoursCreate).RequireAuthorization(StaffPolicies.Admin);
        group.MapGet("/", ListAsync).WithName("OperatingHours.List");
        group.MapGet("/{id}", GetAsync).WithName("OperatingHours.Get");
        group.MapPut("/{id}", UpdateAsync).WithName(AdministrationActions.OperatingHoursUpdate).RequireAuthorization(StaffPolicies.Admin);

        return endpoints;
    }

    private static async Task<IResult> CreateAsync(CreateOperatingHoursRequest request, IOperatingHoursAdministration administration, CancellationToken cancellationToken) =>
        VersionedResult.Created(await administration.CreateAsync(request, cancellationToken), hours => $"{Route}/{hours.Id}", hours => hours.Version);

    /// <summary>
    /// One property's operating hours (<c>?propertyId=</c>, required).
    /// </summary>
    private static async Task<IResult> ListAsync(RecordId propertyId, IOperatingHoursAdministration administration, CancellationToken cancellationToken) =>
        TypedResults.Ok(await administration.ListAsync(new PropertyId(propertyId.Value), cancellationToken));

    private static async Task<IResult> GetAsync(RecordId id, IOperatingHoursAdministration administration, CancellationToken cancellationToken) =>
        VersionedResult.Ok(await administration.GetAsync(id.Value, cancellationToken), hours => hours.Version);

    private static Task<IResult> UpdateAsync(RecordId id, UpdateOperatingHoursRequest request, HttpRequest http, IOperatingHoursAdministration administration, CancellationToken cancellationToken) =>
        VersionedResult.ChangeAsync(
            http,
            expectedVersion => administration.UpdateAsync(id.Value, request, expectedVersion, cancellationToken),
            hours => hours.Version);
}
