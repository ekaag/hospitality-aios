using HospitalityOS.Api.Authentication;
using HospitalityOS.Api.Http;
using HospitalityOS.Contracts.Api.Work;
using HospitalityOS.SharedKernel.Ids;
using HospitalityOS.SharedKernel.Results;

namespace HospitalityOS.Api.Work;

/// <summary>
/// <c>/api/v1/work</c>: the staff surface (J2, spec §32). Staff list a property's work by view and step through it
/// (accept, start, complete or fail; claim unassigned work they are eligible for); managers and administrators also assign
/// work and ask again for work that failed. Each step answers with the work as it now stands.
/// </summary>
internal static class WorkEndpoints
{
    public const string Route = "/api/v1/work";

    public static IServiceCollection AddStaffWork(this IServiceCollection services) => services.AddScoped<StaffWork>();

    public static IEndpointRouteBuilder MapWorkEndpoints(this IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder group = endpoints.MapGroup(Route).RequireAuthorization(StaffPolicies.Staff).WithTags("Work");

        group.MapGet("/", ListAsync).Produces<IReadOnlyList<WorkItemResponse>>().WithName("Work.List");
        group.MapGet("/{id}", (RecordId id, StaffWork work, CancellationToken cancellationToken) => Answer(work.GetAsync(id.Value, cancellationToken)))
            .Produces<WorkItemResponse>().WithName("Work.Get");
        group.MapPost("/{id}/accept", (RecordId id, StaffWork work, CancellationToken cancellationToken) => Answer(work.AcceptAsync(id.Value, cancellationToken)))
            .Produces<WorkItemResponse>().WithName("Work.Accept");
        group.MapPost("/{id}/start", (RecordId id, StaffWork work, CancellationToken cancellationToken) => Answer(work.StartAsync(id.Value, cancellationToken)))
            .Produces<WorkItemResponse>().WithName("Work.Start");
        group.MapPost("/{id}/complete", (RecordId id, CompleteWorkRequest? request, StaffWork work, CancellationToken cancellationToken) =>
            Answer(work.CompleteAsync(id.Value, request ?? new CompleteWorkRequest(), cancellationToken)))
            .Produces<WorkItemResponse>().WithName("Work.Complete");
        group.MapPost("/{id}/fail", (RecordId id, FailWorkRequest request, StaffWork work, CancellationToken cancellationToken) =>
            Answer(work.FailAsync(id.Value, request, cancellationToken)))
            .Produces<WorkItemResponse>().WithName("Work.Fail");
        group.MapPost("/{id}/claim", (RecordId id, StaffWork work, CancellationToken cancellationToken) => Answer(work.ClaimAsync(id.Value, cancellationToken)))
            .Produces<WorkItemResponse>().WithName("Work.Claim");
        group.MapPost("/{id}/assign", (RecordId id, AssignWorkRequest request, StaffWork work, CancellationToken cancellationToken) =>
            Answer(work.AssignAsync(id.Value, request, cancellationToken)))
            .Produces<WorkItemResponse>().WithName("Work.Assign").RequireAuthorization(StaffPolicies.Oversight);
        group.MapPost("/{id}/request-again", (RecordId id, StaffWork work, CancellationToken cancellationToken) =>
            Answer(work.RequestAgainAsync(id.Value, cancellationToken), created: true))
            .Produces<WorkItemResponse>(StatusCodes.Status201Created).WithName("Work.RequestAgain").RequireAuthorization(StaffPolicies.Oversight);

        return endpoints;
    }

    /// <summary>
    /// One view of a property's work (<c>?propertyId=&amp;view=</c>, both required).
    /// </summary>
    private static async Task<IResult> ListAsync(RecordId propertyId, WorkView view, StaffWork work, CancellationToken cancellationToken)
    {
        Result<IReadOnlyList<WorkItemResponse>> listed = await work.ListAsync(new PropertyId(propertyId.Value), view, cancellationToken).ConfigureAwait(false);

        return listed.IsSuccess ? TypedResults.Ok(listed.Value) : listed.Error.ToProblem();
    }

    private static async Task<IResult> Answer(Task<Result<WorkItemResponse>> step, bool created = false)
    {
        Result<WorkItemResponse> done = await step.ConfigureAwait(false);

        return done.IsFailure ? done.Error.ToProblem()
            : created ? TypedResults.Created($"{Route}/{done.Value.Id}", done.Value)
            : TypedResults.Ok(done.Value);
    }
}
