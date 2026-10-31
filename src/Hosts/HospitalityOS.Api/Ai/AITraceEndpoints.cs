using HospitalityOS.Ai.Tracing;
using HospitalityOS.Api.Authentication;
using HospitalityOS.Api.Http;
using HospitalityOS.Contracts.Api.Ai;
using HospitalityOS.SharedKernel.Ids;
using HospitalityOS.SharedKernel.Results;

namespace HospitalityOS.Api.Ai;

/// <summary>
/// <c>/api/v1/ai/traces</c>: what the caller's tenant's AI agents did and why (spec §26). Managers and administrators read;
/// traces are redacted for roles that may not see them whole.
/// </summary>
internal static class AITraceEndpoints
{
    public const string Route = "/api/v1/ai/traces";

    public static IEndpointRouteBuilder MapAITraceEndpoints(this IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder group = endpoints.MapGroup(Route).RequireAuthorization(StaffPolicies.Oversight);

        group.MapGet("/", ListAsync).Produces<AITracePage>().WithName("AITrace.List");
        group.MapGet("/{id}", GetAsync).Produces<AITraceResponse>().WithName("AITrace.Get");

        return endpoints;
    }

    /// <summary>
    /// Traces, newest first, optionally filtered (<c>?propertyId=&amp;correlationId=&amp;outcome=&amp;from=&amp;before=</c>) and
    /// paged (<c>?limit=&amp;after=</c>).
    /// </summary>
    private static async Task<IResult> ListAsync(
        RecordId? propertyId,
        RecordId? correlationId,
        AITraceOutcome? outcome,
        DateTimeOffset? from,
        DateTimeOffset? before,
        string? after,
        int? limit,
        IAITraceReader reader,
        CancellationToken cancellationToken)
    {
        Result<AITracePage> page = await reader.ListAsync(
            new AITraceListRequest(
                propertyId is { } property ? new PropertyId(property.Value) : null,
                correlationId?.Value,
                outcome,
                from,
                before,
                after,
                limit),
            cancellationToken).ConfigureAwait(false);

        return page.IsSuccess ? TypedResults.Ok(page.Value) : page.Error.ToProblem();
    }

    private static async Task<IResult> GetAsync(RecordId id, IAITraceReader reader, CancellationToken cancellationToken)
    {
        Result<AITraceResponse> trace = await reader.GetAsync(id.Value, cancellationToken).ConfigureAwait(false);

        return trace.IsSuccess ? TypedResults.Ok(trace.Value) : trace.Error.ToProblem();
    }
}
