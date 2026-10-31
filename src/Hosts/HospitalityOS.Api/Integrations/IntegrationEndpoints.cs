using HospitalityOS.Api.Authentication;
using HospitalityOS.Api.Http;
using HospitalityOS.Contracts.Api.Integrations;
using HospitalityOS.SharedKernel.Ids;
using HospitalityOS.SharedKernel.Results;

namespace HospitalityOS.Api.Integrations;

/// <summary>
/// <c>/api/v1/integrations</c> (J4, spec §34): what a property and the platform are connected to, for administrators.
/// Read-only: providers, bindings, and models change through configuration.
/// </summary>
internal static class IntegrationEndpoints
{
    public const string Route = "/api/v1/integrations";

    public static IServiceCollection AddPropertyIntegrations(this IServiceCollection services) => services.AddScoped<PropertyIntegrations>();

    public static IEndpointRouteBuilder MapIntegrationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder group = endpoints.MapGroup(Route).RequireAuthorization(StaffPolicies.Admin).WithTags("Integrations");

        group.MapGet("/", GetAsync).Produces<IntegrationsResponse>().WithName("Integration.Get");

        return endpoints;
    }

    /// <summary>
    /// One property's (<c>?propertyId=</c>, required).
    /// </summary>
    private static async Task<IResult> GetAsync(RecordId propertyId, PropertyIntegrations integrations, CancellationToken cancellationToken)
    {
        Result<IntegrationsResponse> found = await integrations.GetAsync(new PropertyId(propertyId.Value), cancellationToken).ConfigureAwait(false);

        return found.IsSuccess ? TypedResults.Ok(found.Value) : found.Error.ToProblem();
    }
}
