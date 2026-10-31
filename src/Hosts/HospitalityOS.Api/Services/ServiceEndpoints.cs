using HospitalityOS.Api.Authentication;
using HospitalityOS.Api.Http;
using HospitalityOS.Application.Administration;
using HospitalityOS.Application.Services;
using HospitalityOS.Contracts.Api.Services;
using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Api.Services;

/// <summary>
/// <c>/api/v1/services</c>: what the caller's tenant's properties do for guests, and what guests may request. Staff read;
/// administrators define and update.
/// </summary>
internal static class ServiceEndpoints
{
    public const string Route = "/api/v1/services";

    public static IEndpointRouteBuilder MapServiceEndpoints(this IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder group = endpoints.MapGroup(Route).RequireAuthorization(StaffPolicies.Staff);

        group.MapPost("/", CreateAsync).Produces<ServiceResponse>(StatusCodes.Status201Created).WithName(AdministrationActions.ServiceCreate).RequireAuthorization(StaffPolicies.Admin);
        group.MapGet("/", ListAsync).Produces<IReadOnlyList<ServiceResponse>>().WithName("Service.List");
        group.MapGet("/{id}", GetAsync).Produces<ServiceResponse>().WithName("Service.Get");
        group.MapPut("/{id}", UpdateAsync).Produces<ServiceResponse>().WithName(AdministrationActions.ServiceUpdate).RequireAuthorization(StaffPolicies.Admin);

        return endpoints;
    }

    private static async Task<IResult> CreateAsync(CreateServiceRequest request, IServiceAdministration administration, CancellationToken cancellationToken) =>
        VersionedResult.Created(await administration.CreateAsync(request, cancellationToken), service => $"{Route}/{service.Id}", service => service.Version);

    /// <summary>
    /// One property's services (<c>?propertyId=</c>, required).
    /// </summary>
    private static async Task<IResult> ListAsync(RecordId propertyId, IServiceAdministration administration, CancellationToken cancellationToken) =>
        TypedResults.Ok(await administration.ListAsync(new PropertyId(propertyId.Value), cancellationToken));

    private static async Task<IResult> GetAsync(RecordId id, IServiceAdministration administration, CancellationToken cancellationToken) =>
        VersionedResult.Ok(await administration.GetAsync(id.Value, cancellationToken), service => service.Version);

    private static Task<IResult> UpdateAsync(RecordId id, UpdateServiceRequest request, HttpRequest http, IServiceAdministration administration, CancellationToken cancellationToken) =>
        VersionedResult.ChangeAsync(
            http,
            expectedVersion => administration.UpdateAsync(id.Value, request, expectedVersion, cancellationToken),
            service => service.Version);
}
