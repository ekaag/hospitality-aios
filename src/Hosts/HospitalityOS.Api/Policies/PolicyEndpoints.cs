using HospitalityOS.Api.Authentication;
using HospitalityOS.Api.Http;
using HospitalityOS.Application.Administration;
using HospitalityOS.Application.Policies;
using HospitalityOS.Contracts.Api.Policies;
using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Api.Policies;

/// <summary>
/// <c>/api/v1/policies</c>: the policies of the caller's tenant's properties (Journey 4). Staff read; administrators
/// draft, edit, activate, and retire.
/// </summary>
internal static class PolicyEndpoints
{
    public const string Route = "/api/v1/policies";

    public static IEndpointRouteBuilder MapPolicyEndpoints(this IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder group = endpoints.MapGroup(Route).RequireAuthorization(StaffPolicies.Staff);

        group.MapPost("/", CreateAsync).Produces<PolicyResponse>(StatusCodes.Status201Created).WithName(AdministrationActions.PolicyCreate).RequireAuthorization(StaffPolicies.Admin);
        group.MapGet("/", ListAsync).Produces<IReadOnlyList<PolicyResponse>>().WithName("Policy.List");
        group.MapGet("/{id}", GetAsync).Produces<PolicyResponse>().WithName("Policy.Get");
        group.MapPut("/{id}", UpdateAsync).Produces<PolicyResponse>().WithName(AdministrationActions.PolicyUpdate).RequireAuthorization(StaffPolicies.Admin);
        group.MapPost("/{id}/activate", ActivateAsync).Produces<PolicyResponse>().WithName(AdministrationActions.PolicyActivate).RequireAuthorization(StaffPolicies.Admin);
        group.MapPost("/{id}/retire", RetireAsync).Produces<PolicyResponse>().WithName(AdministrationActions.PolicyRetire).RequireAuthorization(StaffPolicies.Admin);

        return endpoints;
    }

    private static async Task<IResult> CreateAsync(CreatePolicyRequest request, IPolicyAdministration administration, CancellationToken cancellationToken) =>
        VersionedResult.Created(await administration.CreateAsync(request, cancellationToken), policy => $"{Route}/{policy.Id}", policy => policy.Version);

    /// <summary>
    /// One property's policies in every status (<c>?propertyId=</c>, required).
    /// </summary>
    private static async Task<IResult> ListAsync(RecordId propertyId, IPolicyAdministration administration, CancellationToken cancellationToken) =>
        TypedResults.Ok(await administration.ListAsync(new PropertyId(propertyId.Value), cancellationToken));

    private static async Task<IResult> GetAsync(RecordId id, IPolicyAdministration administration, CancellationToken cancellationToken) =>
        VersionedResult.Ok(await administration.GetAsync(id.Value, cancellationToken), policy => policy.Version);

    private static Task<IResult> UpdateAsync(RecordId id, UpdatePolicyRequest request, HttpRequest http, IPolicyAdministration administration, CancellationToken cancellationToken) =>
        VersionedResult.ChangeAsync(
            http,
            expectedVersion => administration.UpdateAsync(id.Value, request, expectedVersion, cancellationToken),
            policy => policy.Version);

    private static Task<IResult> ActivateAsync(RecordId id, HttpRequest http, IPolicyAdministration administration, CancellationToken cancellationToken) =>
        VersionedResult.ChangeAsync(http, expectedVersion => administration.ActivateAsync(id.Value, expectedVersion, cancellationToken), policy => policy.Version);

    private static Task<IResult> RetireAsync(RecordId id, HttpRequest http, IPolicyAdministration administration, CancellationToken cancellationToken) =>
        VersionedResult.ChangeAsync(http, expectedVersion => administration.RetireAsync(id.Value, expectedVersion, cancellationToken), policy => policy.Version);
}
