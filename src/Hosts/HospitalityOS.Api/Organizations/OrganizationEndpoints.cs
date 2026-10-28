using HospitalityOS.Api.Authentication;
using HospitalityOS.Api.Http;
using HospitalityOS.Application.Administration;
using HospitalityOS.Application.Organizations;
using HospitalityOS.Contracts.Api.Organizations;
using HospitalityOS.SharedKernel.Ids;
using HospitalityOS.SharedKernel.Results;

namespace HospitalityOS.Api.Organizations;

/// <summary>
/// <c>/api/v1/organizations</c>: the caller's tenant's organizations. Staff read; administrators create and rename.
/// </summary>
internal static class OrganizationEndpoints
{
    public const string Route = "/api/v1/organizations";

    public static IEndpointRouteBuilder MapOrganizationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder group = endpoints.MapGroup(Route).RequireAuthorization(StaffPolicies.Staff);

        group.MapPost("/", CreateAsync).WithName(AdministrationActions.OrganizationCreate).RequireAuthorization(StaffPolicies.Admin);
        group.MapGet("/", ListAsync).WithName("Organization.List");
        group.MapGet("/{id:guid}", GetAsync).WithName("Organization.Get");
        group.MapPut("/{id:guid}", RenameAsync).WithName(AdministrationActions.OrganizationRename).RequireAuthorization(StaffPolicies.Admin);

        return endpoints;
    }

    private static async Task<IResult> CreateAsync(CreateOrganizationRequest request, IOrganizationAdministration administration, CancellationToken cancellationToken)
    {
        Result<OrganizationResponse> created = await administration.CreateAsync(request, cancellationToken);

        return created.IsFailure ? created.Error.ToProblem() : new VersionedResult(TypedResults.Created($"{Route}/{created.Value.Id}", created.Value), created.Value.Version);
    }

    private static async Task<IResult> ListAsync(IOrganizationAdministration administration, CancellationToken cancellationToken) =>
        TypedResults.Ok(await administration.ListAsync(cancellationToken));

    private static async Task<IResult> GetAsync(Guid id, IOrganizationAdministration administration, CancellationToken cancellationToken)
    {
        Result<OrganizationResponse> found = await administration.GetAsync(new OrganizationId(id), cancellationToken);

        return found.IsFailure ? found.Error.ToProblem() : new VersionedResult(TypedResults.Ok(found.Value), found.Value.Version);
    }

    private static async Task<IResult> RenameAsync(Guid id, RenameOrganizationRequest request, HttpRequest http, IOrganizationAdministration administration, CancellationToken cancellationToken)
    {
        Result<long?> expected = Versions.ExpectedVersion(http);

        if (expected.IsFailure)
        {
            return expected.Error.ToProblem();
        }

        Result<OrganizationResponse> renamed = await administration.RenameAsync(new OrganizationId(id), request, expected.Value, cancellationToken);

        return renamed.IsFailure ? renamed.Error.ToProblem() : new VersionedResult(TypedResults.Ok(renamed.Value), renamed.Value.Version);
    }
}
