using HospitalityOS.Api.Authentication;
using HospitalityOS.Api.Http;
using HospitalityOS.Application.Administration;
using HospitalityOS.Application.Properties;
using HospitalityOS.Contracts.Api.Properties;
using HospitalityOS.SharedKernel.Ids;
using HospitalityOS.SharedKernel.Results;

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
        group.MapGet("/{id:guid}", GetAsync).WithName("Property.Get");
        group.MapPut("/{id:guid}", UpdateAsync).WithName(AdministrationActions.PropertyUpdate).RequireAuthorization(StaffPolicies.Admin);

        return endpoints;
    }

    private static async Task<IResult> CreateAsync(CreatePropertyRequest request, IPropertyAdministration administration, CancellationToken cancellationToken)
    {
        Result<PropertyResponse> created = await administration.CreateAsync(request, cancellationToken);

        return created.IsFailure ? created.Error.ToProblem() : new VersionedResult(TypedResults.Created($"{Route}/{created.Value.Id}", created.Value), created.Value.Version);
    }

    /// <summary>
    /// All the tenant's properties, or one organization's (<c>?organizationId=</c>).
    /// </summary>
    private static async Task<IResult> ListAsync(Guid? organizationId, IPropertyAdministration administration, CancellationToken cancellationToken) =>
        TypedResults.Ok(await administration.ListAsync(organizationId is { } id ? new OrganizationId(id) : null, cancellationToken));

    private static async Task<IResult> GetAsync(Guid id, IPropertyAdministration administration, CancellationToken cancellationToken)
    {
        Result<PropertyResponse> found = await administration.GetAsync(new PropertyId(id), cancellationToken);

        return found.IsFailure ? found.Error.ToProblem() : new VersionedResult(TypedResults.Ok(found.Value), found.Value.Version);
    }

    private static async Task<IResult> UpdateAsync(Guid id, UpdatePropertyRequest request, HttpRequest http, IPropertyAdministration administration, CancellationToken cancellationToken)
    {
        Result<long?> expected = Versions.ExpectedVersion(http);

        if (expected.IsFailure)
        {
            return expected.Error.ToProblem();
        }

        Result<PropertyResponse> updated = await administration.UpdateAsync(new PropertyId(id), request, expected.Value, cancellationToken);

        return updated.IsFailure ? updated.Error.ToProblem() : new VersionedResult(TypedResults.Ok(updated.Value), updated.Value.Version);
    }
}
