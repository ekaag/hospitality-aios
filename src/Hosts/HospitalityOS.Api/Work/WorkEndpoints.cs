using HospitalityOS.Api.Authentication;
using HospitalityOS.Api.Http;
using HospitalityOS.Application.Evidence;
using HospitalityOS.Contracts.Api.Work;
using HospitalityOS.Ports.Storage;
using HospitalityOS.SharedKernel.Ids;
using HospitalityOS.SharedKernel.Results;
using Microsoft.AspNetCore.Mvc;

namespace HospitalityOS.Api.Work;

/// <summary>
/// <c>/api/v1/work</c>: the staff surface (J2, spec §32). Staff list a property's work by view and step through it
/// (accept, start, complete or fail; claim unassigned work they are eligible for); managers and administrators also assign
/// work and ask again for work that failed. Each step answers with the work as it now stands. The worker uploads photos or
/// files as evidence (multipart, field <c>file</c>) and completes the work with their keys; whoever sees the work can read them.
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
        group.MapPost("/{id}/evidence", AddEvidenceAsync)
            .Accepts<IFormFile>("multipart/form-data")
            .Produces<EvidenceResponse>(StatusCodes.Status201Created)
            .WithName("Work.AddEvidence")
            .DisableAntiforgery()
            .WithMetadata(new RequestSizeLimitAttribute(HumanActionEvidence.MaxBytes + UploadOverhead));
        group.MapGet("/{id}/evidence/{file}", GetEvidenceAsync)
            .Produces<byte[]>(StatusCodes.Status200OK, "application/octet-stream")
            .WithName("Work.GetEvidence");
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

    /// <summary>
    /// Room in a request for the multipart framing around a file of the largest size.
    /// </summary>
    private const long UploadOverhead = 64 * 1024;

    private static async Task<IResult> AddEvidenceAsync(RecordId id, IFormFile file, StaffWork work, CancellationToken cancellationToken)
    {
        Stream content = file.OpenReadStream();

        await using (content.ConfigureAwait(false))
        {
            Result<EvidenceResponse> added = await work.AddEvidenceAsync(id.Value, content, file.ContentType, file.Length, cancellationToken).ConfigureAwait(false);

            return added.IsFailure
                ? added.Error.ToProblem()
                : TypedResults.Created($"{Route}/{id.Value}/evidence/{added.Value.Key.Split('/')[1]}", added.Value);
        }
    }

    private static async Task<IResult> GetEvidenceAsync(RecordId id, string file, StaffWork work, CancellationToken cancellationToken)
    {
        Result<StoredBlob> opened = await work.OpenEvidenceAsync(id.Value, file, cancellationToken).ConfigureAwait(false);

        // The stream result disposes the content once it is written.
        return opened.IsFailure ? opened.Error.ToProblem() : TypedResults.Stream(opened.Value.Content, opened.Value.ContentType);
    }

    private static async Task<IResult> Answer(Task<Result<WorkItemResponse>> step, bool created = false)
    {
        Result<WorkItemResponse> done = await step.ConfigureAwait(false);

        return done.IsFailure ? done.Error.ToProblem()
            : created ? TypedResults.Created($"{Route}/{done.Value.Id}", done.Value)
            : TypedResults.Ok(done.Value);
    }
}
