using HospitalityOS.Api.Authentication;
using HospitalityOS.Api.Http;
using HospitalityOS.Application.Administration;
using HospitalityOS.Application.GuestLinks;
using HospitalityOS.Contracts.Api.GuestSessions;
using HospitalityOS.Contracts.Api.Stays;
using HospitalityOS.Ports.Bindings;
using HospitalityOS.Ports.Security;
using HospitalityOS.SharedKernel.Ids;
using HospitalityOS.SharedKernel.Results;
using Microsoft.Extensions.Options;

namespace HospitalityOS.Api.Stays;

/// <summary>
/// Check-in links (K4c).
/// <list type="bullet">
/// <item><c>/api/v1/stays</c>: managers and administrators (the front desk) see the property's reserved and in-house
/// stays, and create (replacing) or revoke a stay's check-in link.</item>
/// <item><c>/api/v1/guest-sessions</c>: anyone with a link's code gets a stay token for its stay (anonymous; the code is
/// the credential).</item>
/// </list>
/// </summary>
internal static class StayEndpoints
{
    public const string Route = "/api/v1/stays";
    public const string SessionsRoute = "/api/v1/guest-sessions";

    public static IEndpointRouteBuilder MapStayEndpoints(this IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder stays = endpoints.MapGroup(Route).WithTags("Stays").RequireAuthorization(StaffPolicies.Oversight);
        stays.MapGet("/", ListAsync).Produces<IReadOnlyList<StayResponse>>().WithName("Stay.List");
        stays.MapPost("/{id}/guest-link", CreateLinkAsync).Produces<GuestLinkResponse>().WithName(AdministrationActions.GuestLinkCreate);
        stays.MapDelete("/{id}/guest-link", RevokeLinkAsync).Produces<StayResponse>().WithName(AdministrationActions.GuestLinkRevoke);

        endpoints.MapPost(SessionsRoute, OpenSessionAsync).WithTags("Stays").AllowAnonymous().Produces<StayTokenResponse>().WithName("GuestSession.Open");

        return endpoints;
    }

    /// <summary>
    /// One property's stays (<c>?propertyId=</c>, required).
    /// </summary>
    private static async Task<IResult> ListAsync(RecordId propertyId, IGuestLinks links, CancellationToken cancellationToken) =>
        TypedResults.Ok(await links.ListStaysAsync(new PropertyId(propertyId.Value), cancellationToken));

    private static async Task<IResult> CreateLinkAsync(RecordId id, IGuestLinks links, IOptions<GuestLinkOptions> options, CancellationToken cancellationToken)
    {
        Result<CreatedGuestLink> created = await links.CreateAsync(id.Value, cancellationToken);

        return created.IsSuccess
            ? TypedResults.Ok(new GuestLinkResponse(created.Value.StayId, created.Value.Code, options.Value.UrlFor(created.Value.Code)))
            : created.Error.ToProblem();
    }

    private static async Task<IResult> RevokeLinkAsync(RecordId id, IGuestLinks links, CancellationToken cancellationToken)
    {
        Result<StayResponse> revoked = await links.RevokeAsync(id.Value, cancellationToken);
        return revoked.IsSuccess ? TypedResults.Ok(revoked.Value) : revoked.Error.ToProblem();
    }

    private static async Task<IResult> OpenSessionAsync(
        OpenGuestSessionRequest request, IGuestSessions sessions, IBindingResolver<ISigningKey> keys, CancellationToken cancellationToken)
    {
        Result<GuestSession> session = await sessions.OpenAsync(request.Code, cancellationToken);

        // The bearer handler checks lifetimes against the system clock, so tokens are issued by it too.
        return session.IsSuccess
            ? TypedResults.Ok(await StayTokens.IssueAsync(session.Value, keys.Resolve(), TimeProvider.System, cancellationToken))
            : session.Error.ToProblem();
    }
}

/// <summary>
/// <c>GuestLinks</c>: <c>BaseUrl</c>, where the guest app is (e.g. <c>http://localhost:5173</c>), so a new link comes as
/// an address to open or encode as a QR code; without it, only the code. The code goes in the address's fragment, which
/// browsers never send to a server, so it stays out of logs.
/// </summary>
internal sealed class GuestLinkOptions
{
    public const string SectionName = "GuestLinks";

    public Uri? BaseUrl { get; set; }

    public string? UrlFor(string code) => BaseUrl is null ? null : $"{BaseUrl.GetLeftPart(UriPartial.Path).TrimEnd('/')}/#link={Uri.EscapeDataString(code)}";
}
