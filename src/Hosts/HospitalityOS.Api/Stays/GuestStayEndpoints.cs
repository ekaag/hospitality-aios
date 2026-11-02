using System.Security.Claims;
using HospitalityOS.Api.Authentication;
using HospitalityOS.Api.Http;
using HospitalityOS.Application.Conversations;
using HospitalityOS.Contracts.Api.Stays;
using HospitalityOS.Domain.Guests.StayContexts;
using HospitalityOS.Domain.Properties.AccommodationUnits;
using HospitalityOS.Domain.Properties.Properties;
using HospitalityOS.SharedKernel.Tenancy;

namespace HospitalityOS.Api.Stays;

/// <summary>
/// <c>/api/v1/stay</c> for guests: the caller's own stay (which hotel, which room, and when), with a stay token, while
/// the stay is active. The guest app names the hotel and dresses itself in its look from this.
/// </summary>
internal static class GuestStayEndpoints
{
    public const string Route = "/api/v1/stay";

    public static IEndpointRouteBuilder MapGuestStayEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(Route, GetAsync).RequireAuthorization(GuestPolicies.Guest).WithTags("Stays").Produces<GuestStayResponse>().WithName("Stay.GetMine");

        return endpoints;
    }

    private static async Task<IResult> GetAsync(
        ClaimsPrincipal user,
        IStayContextRepository stays,
        IPropertyRepository properties,
        IAccommodationUnitRepository units,
        CancellationToken cancellationToken)
    {
        GuestCaller caller = Caller(user);
        StayContext? stay = await stays.GetByIdAsync(caller.StayContextId, cancellationToken).ConfigureAwait(false);

        if (!caller.IsActive(stay))
        {
            return ConversationErrors.StayNotActive.ToProblem();
        }

        Property? property = await properties.GetByIdAsync(stay!.PropertyId, cancellationToken).ConfigureAwait(false);
        AccommodationUnit? unit = stay.AccommodationUnitId is { } unitId ? await units.GetByIdAsync(unitId, cancellationToken).ConfigureAwait(false) : null;

        return TypedResults.Ok(new GuestStayResponse(stay.Id, stay.PropertyId, property?.Details.Name ?? "", unit?.UnitNumber, stay.Arrival, stay.Departure));
    }

    // The guest policy admitted the caller, and authentication refuses incomplete guest tokens, so the claims are complete.
    private static GuestCaller Caller(ClaimsPrincipal user) =>
        GuestClaims.TryRead(user, out TenantId _, out GuestCaller? caller, out string? problem) ? caller! : throw new InvalidOperationException(problem);
}
