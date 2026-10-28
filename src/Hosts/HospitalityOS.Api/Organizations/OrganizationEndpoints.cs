using HospitalityOS.Api.Authentication;
using HospitalityOS.Api.Http;
using HospitalityOS.Application.Administration;
using HospitalityOS.Application.Organizations;
using HospitalityOS.Contracts.Api.Organizations;
using HospitalityOS.SharedKernel.Ids;

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
        group.MapGet("/{id}", GetAsync).WithName("Organization.Get");
        group.MapPut("/{id}", RenameAsync).WithName(AdministrationActions.OrganizationRename).RequireAuthorization(StaffPolicies.Admin);

        return endpoints;
    }

    private static async Task<IResult> CreateAsync(CreateOrganizationRequest request, IOrganizationAdministration administration, CancellationToken cancellationToken) =>
        VersionedResult.Created(await administration.CreateAsync(request, cancellationToken), organization => $"{Route}/{organization.Id}", organization => organization.Version);

    private static async Task<IResult> ListAsync(IOrganizationAdministration administration, CancellationToken cancellationToken) =>
        TypedResults.Ok(await administration.ListAsync(cancellationToken));

    private static async Task<IResult> GetAsync(RecordId id, IOrganizationAdministration administration, CancellationToken cancellationToken) =>
        VersionedResult.Ok(await administration.GetAsync(new OrganizationId(id.Value), cancellationToken), organization => organization.Version);

    private static Task<IResult> RenameAsync(RecordId id, RenameOrganizationRequest request, HttpRequest http, IOrganizationAdministration administration, CancellationToken cancellationToken) =>
        VersionedResult.ChangeAsync(
            http,
            expectedVersion => administration.RenameAsync(new OrganizationId(id.Value), request, expectedVersion, cancellationToken),
            organization => organization.Version);
}
