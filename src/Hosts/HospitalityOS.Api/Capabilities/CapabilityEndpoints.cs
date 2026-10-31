using HospitalityOS.Capabilities.Definitions;
using HospitalityOS.Capabilities.Registry;
using HospitalityOS.Contracts.Capabilities;
using Microsoft.AspNetCore.Http.HttpResults;

namespace HospitalityOS.Api.Capabilities;

/// <summary>
/// <c>/api/v1/capabilities</c>: the registered capabilities with every spec §11 field. Read-only; the definitions are
/// code, not data.
/// </summary>
internal static class CapabilityEndpoints
{
    public const string Route = "/api/v1/capabilities";

    public static IEndpointRouteBuilder MapCapabilityEndpoints(this IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder group = endpoints.MapGroup(Route);

        group.MapGet("/", List).WithName("Capability.List");
        group.MapGet("/{name}", Get).WithName("Capability.Get");

        return endpoints;
    }

    private static Ok<List<CapabilityDescription>> List(CapabilityRegistry registry) =>
        TypedResults.Ok(registry.All.Select(definition => definition.Describe()).ToList());

    /// <summary>
    /// The capability at <paramref name="version"/>, or its latest version.
    /// </summary>
    private static Results<Ok<CapabilityDescription>, NotFound> Get(string name, int? version, CapabilityRegistry registry) =>
        registry.Find(name, version) is CapabilityDefinition definition
            ? TypedResults.Ok(definition.Describe())
            : TypedResults.NotFound();
}
