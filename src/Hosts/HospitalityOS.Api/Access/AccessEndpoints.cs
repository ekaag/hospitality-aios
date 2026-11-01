using System.Security.Claims;
using HospitalityOS.Api.Authentication;
using HospitalityOS.Api.Http;
using HospitalityOS.Application.Access;
using HospitalityOS.Application.Administration;
using HospitalityOS.Contracts.Api.Access;
using HospitalityOS.SharedKernel.Execution;
using HospitalityOS.SharedKernel.Results;
using HospitalityOS.SharedKernel.Tenancy;

namespace HospitalityOS.Api.Access;

/// <summary>
/// Who may sign in, and as whom (K4b).
/// <list type="bullet">
/// <item><c>/api/v1/me</c>: who the caller acts as, and redeeming an invite to link their sign-in.</item>
/// <item><c>/api/v1/access</c>: administrators invite people, send new codes, and revoke access.</item>
/// <item><c>/api/v1/operator/tenants/{tenantId}/admins</c>: operators invite a customer's first administrator.</item>
/// </list>
/// </summary>
internal static class AccessEndpoints
{
    public const string MeRoute = "/api/v1/me";
    public const string Route = "/api/v1/access";
    public const string OperatorRoute = "/api/v1/operator/tenants";

    public static IEndpointRouteBuilder MapAccessEndpoints(this IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder me = endpoints.MapGroup(MeRoute).WithTags("Access");
        me.MapGet("/", GetMeAsync).Produces<SignInResponse>().WithName("Me.Get").RequireAuthorization(SignInPolicies.Caller);
        me.MapPost("/invite", RedeemAsync).Produces<StaffAccessResponse>().WithName(AdministrationActions.StaffAccessRedeem).RequireAuthorization(SignInPolicies.SignIn);

        RouteGroupBuilder access = endpoints.MapGroup(Route).WithTags("Access").RequireAuthorization(StaffPolicies.Admin);
        access.MapPost("/", GrantAsync).Produces<StaffInviteResponse>(StatusCodes.Status201Created).WithName(AdministrationActions.StaffAccessGrant);
        access.MapGet("/", ListAsync).Produces<IReadOnlyList<StaffAccessResponse>>().WithName("StaffAccess.List");
        access.MapGet("/{id}", GetAsync).Produces<StaffAccessResponse>().WithName("StaffAccess.Get");
        access.MapPost("/{id}/invite", ReinviteAsync).Produces<StaffInviteResponse>().WithName(AdministrationActions.StaffAccessReinvite);
        access.MapPost("/{id}/revoke", RevokeAsync).Produces<StaffAccessResponse>().WithName(AdministrationActions.StaffAccessRevoke);

        endpoints.MapGroup(OperatorRoute).WithTags("Access").RequireAuthorization(SignInPolicies.Operator)
            .MapPost("/{tenantId}/admins", GrantFirstAdminAsync)
            .Produces<StaffInviteResponse>(StatusCodes.Status201Created)
            .WithName(AdministrationActions.StaffAccessGrantFirstAdmin);

        return endpoints;
    }

    private static async Task<IResult> GetMeAsync(ClaimsPrincipal user, IStaffAccessAdministration administration, CancellationToken cancellationToken)
    {
        bool isOperator = user.IsInRole(SignInClaims.OperatorRole);

        if (!StaffClaims.TryRead(user, out TenantId tenant, out Actor actor, out _))
        {
            return TypedResults.Ok(new SignInResponse(false, isOperator, null, null, null, null, null));
        }

        // Staff tokens (local dev tokens) name no access, so only the token's own claims are known.
        StaffAccessResponse? linked = SignInClaims.AccessId(user) is { } accessId
            && await administration.GetAsync(accessId, cancellationToken) is { IsSuccess: true } found
            ? found.Value
            : null;

        return TypedResults.Ok(new SignInResponse(true, isOperator, tenant.Value, linked?.Id, linked?.DisplayName, actor.Role, linked?.StaffMemberId));
    }

    private static async Task<IResult> RedeemAsync(RedeemInviteRequest request, ClaimsPrincipal user, IStaffSignIns signIns, CancellationToken cancellationToken)
    {
        SignInClaims.TryRead(user, out string issuer, out string subject);
        Result<StaffAccessResponse> redeemed = await signIns.RedeemAsync(request.Code, issuer, subject, cancellationToken);
        return redeemed.IsSuccess ? TypedResults.Ok(redeemed.Value) : redeemed.Error.ToProblem();
    }

    private static async Task<IResult> GrantAsync(GrantStaffAccessRequest request, IStaffAccessAdministration administration, CancellationToken cancellationToken) =>
        Created(await administration.GrantAsync(request, cancellationToken));

    private static async Task<IResult> ListAsync(IStaffAccessAdministration administration, CancellationToken cancellationToken) =>
        TypedResults.Ok(await administration.ListAsync(cancellationToken));

    private static async Task<IResult> GetAsync(RecordId id, IStaffAccessAdministration administration, CancellationToken cancellationToken) =>
        VersionedResult.Ok(await administration.GetAsync(id.Value, cancellationToken), access => access.Version);

    private static async Task<IResult> ReinviteAsync(RecordId id, IStaffAccessAdministration administration, CancellationToken cancellationToken)
    {
        Result<StaffInviteResponse> invited = await administration.ReinviteAsync(id.Value, cancellationToken);
        return invited.IsSuccess ? TypedResults.Ok(invited.Value) : invited.Error.ToProblem();
    }

    private static async Task<IResult> RevokeAsync(RecordId id, IStaffAccessAdministration administration, CancellationToken cancellationToken) =>
        VersionedResult.Ok(await administration.RevokeAsync(id.Value, cancellationToken), access => access.Version);

    private static async Task<IResult> GrantFirstAdminAsync(
        RecordId tenantId, GrantFirstAdminRequest request, ClaimsPrincipal user, IStaffSignIns signIns, CancellationToken cancellationToken)
    {
        SignInClaims.TryRead(user, out _, out string subject);
        return Created(await signIns.GrantFirstAdminAsync(new TenantId(tenantId.Value), request, $"operator:{subject}", cancellationToken));
    }

    private static IResult Created(Result<StaffInviteResponse> invited) =>
        invited.IsSuccess ? TypedResults.Created($"{Route}/{invited.Value.Access.Id}", invited.Value) : invited.Error.ToProblem();
}
