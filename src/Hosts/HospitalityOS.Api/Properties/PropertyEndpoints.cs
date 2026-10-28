using HospitalityOS.Api.Authentication;
using HospitalityOS.Api.Http;
using HospitalityOS.Application.Administration;
using HospitalityOS.Application.Properties;
using HospitalityOS.Contracts.Api.Properties;
using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Api.Properties;

/// <summary>
/// <c>/api/v1/properties</c>: the caller's tenant's properties (Journey 1). Staff read; administrators create and update.
/// </summary>
internal static class PropertyEndpoints
{
    public const string Route = "/api/v1/properties";

    public static IEndpointRouteBuilder MapPropertyEndpoints(this IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder group = endpoints.MapGroup(Route).RequireAuthorization(StaffPolicies.Staff);

        group.MapPost("/", CreateAsync).WithName(AdministrationActions.PropertyCreate).RequireAuthorization(StaffPolicies.Admin);
        group.MapGet("/", ListAsync).WithName("Property.List");
        group.MapGet("/{id}", GetAsync).WithName("Property.Get");
        group.MapPut("/{id}", UpdateAsync).WithName(AdministrationActions.PropertyUpdate).RequireAuthorization(StaffPolicies.Admin);

        return endpoints;
    }

    private static async Task<IResult> CreateAsync(CreatePropertyRequest request, IPropertyAdministration administration, CancellationToken cancellationToken) =>
        VersionedResult.Created(await administration.CreateAsync(request, cancellationToken), property => $"{Route}/{property.Id}", property => property.Version);

    /// <summary>
    /// All the tenant's properties, or one organization's (<c>?organizationId=</c>).
    /// </summary>
    private static async Task<IResult> ListAsync(RecordId? organizationId, IPropertyAdministration administration, CancellationToken cancellationToken) =>
        TypedResults.Ok(await administration.ListAsync(organizationId is { } id ? new OrganizationId(id.Value) : null, cancellationToken));

    private static async Task<IResult> GetAsync(RecordId id, IPropertyAdministration administration, CancellationToken cancellationToken) =>
        VersionedResult.Ok(await administration.GetAsync(new PropertyId(id.Value), cancellationToken), property => property.Version);

    private static Task<IResult> UpdateAsync(RecordId id, UpdatePropertyRequest request, HttpRequest http, IPropertyAdministration administration, CancellationToken cancellationToken) =>
        VersionedResult.ChangeAsync(
            http,
            expectedVersion => administration.UpdateAsync(new PropertyId(id.Value), request, expectedVersion, cancellationToken),
            property => property.Version);
}
