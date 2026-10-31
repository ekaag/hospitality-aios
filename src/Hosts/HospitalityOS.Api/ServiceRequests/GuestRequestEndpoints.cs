using System.Security.Claims;
using HospitalityOS.Api.Authentication;
using HospitalityOS.Api.Http;
using HospitalityOS.Application.Conversations;
using HospitalityOS.Contracts.Api.ServiceRequests;
using HospitalityOS.SharedKernel.Results;
using HospitalityOS.SharedKernel.Tenancy;

namespace HospitalityOS.Api.ServiceRequests;

/// <summary>
/// <c>/api/v1/service-requests</c> for guests (J3): the caller's stay's unresolved requests, with a stay token, newest
/// first. Requests are made in the conversation; this is where the guest sees where they are.
/// </summary>
internal static class GuestRequestEndpoints
{
    public const string Route = "/api/v1/service-requests";

    public static IServiceCollection AddGuestRequests(this IServiceCollection services) => services.AddScoped<GuestRequests>();

    public static IEndpointRouteBuilder MapGuestRequestEndpoints(this IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder group = endpoints.MapGroup(Route).RequireAuthorization(GuestPolicies.Guest).WithTags("ServiceRequests");

        group.MapGet("/", ListAsync).Produces<IReadOnlyList<GuestRequestResponse>>().WithName("ServiceRequest.ListMine");

        return endpoints;
    }

    private static async Task<IResult> ListAsync(ClaimsPrincipal user, GuestRequests requests, CancellationToken cancellationToken)
    {
        Result<IReadOnlyList<GuestRequestResponse>> listed = await requests.ListAsync(Caller(user), cancellationToken).ConfigureAwait(false);

        return listed.IsSuccess ? TypedResults.Ok(listed.Value) : listed.Error.ToProblem();
    }

    // The guest policy admitted the caller, and authentication refuses incomplete guest tokens, so the claims are complete.
    private static GuestCaller Caller(ClaimsPrincipal user) =>
        GuestClaims.TryRead(user, out TenantId _, out GuestCaller? caller, out string? problem) ? caller! : throw new InvalidOperationException(problem);
}
