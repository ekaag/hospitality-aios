using HospitalityOS.Api.Authentication;
using HospitalityOS.Api.Http;
using HospitalityOS.Application.Auditing;
using HospitalityOS.Contracts.Api.FailedEvents;
using HospitalityOS.Eventing.Failures;
using HospitalityOS.Ports.Auditing;
using HospitalityOS.Ports.Persistence;
using HospitalityOS.Ports.Tenancy;
using HospitalityOS.SharedKernel.Results;

namespace HospitalityOS.Api.Eventing;

/// <summary>
/// <c>/api/v1/failed-events</c>: the caller's tenant's events whose processing was given up on, and retrying them (L1,
/// spec §42). Administrators only; every retry is audited.
/// </summary>
internal static class FailedEventEndpoints
{
    public const string Route = "/api/v1/failed-events";
    public const string RetryAction = "event.retry";

    public static IEndpointRouteBuilder MapFailedEventEndpoints(this IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder group = endpoints.MapGroup(Route).RequireAuthorization(StaffPolicies.Admin);

        group.MapGet("/", ListAsync).Produces<FailedEventResponse[]>().WithName("FailedEvent.List");
        group.MapPost("/{eventId:guid}/retry", RetryAsync).Produces<FailedEventRetriedResponse>().WithName("FailedEvent.Retry");

        return endpoints;
    }

    private static async Task<IResult> ListAsync(IFailedEvents failedEvents, TenantContext tenant, CancellationToken cancellationToken)
    {
        IReadOnlyList<FailedEvent> failed = await failedEvents.ListAsync(tenant.TenantId, cancellationToken).ConfigureAwait(false);

        return TypedResults.Ok(failed.Select(ToResponse).ToArray());
    }

    private static async Task<IResult> RetryAsync(
        Guid eventId,
        RetryFailedEventRequest? request,
        IFailedEvents failedEvents,
        TenantContext tenant,
        IAuditTrail audit,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        string? handler = string.IsNullOrWhiteSpace(request?.Handler) ? null : request.Handler.Trim();

        if (!await failedEvents.RetryAsync(tenant.TenantId, eventId, handler, cancellationToken).ConfigureAwait(false))
        {
            return Error.NotFound("failed_event.not_found", handler is null
                ? $"Event {eventId} is not waiting to be published again."
                : $"Event {eventId} has not failed in handler {handler}.").ToProblem();
        }

        audit.Record(RetryAction, AuditOutcome.Succeeded, new AuditTarget("Event", eventId.ToString()), handler is null ? "publish" : "handler " + handler);
        Result<CommitReceipt> committed = await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);

        if (committed.IsFailure)
        {
            return committed.Error.ToProblem();
        }

        return TypedResults.Ok(new FailedEventRetriedResponse(eventId, handler is null ? FailedEventStage.Publish : FailedEventStage.Handle, handler));
    }

    private static FailedEventResponse ToResponse(FailedEvent failed) =>
        new(
            failed.Message.EventId,
            failed.Message.EventType,
            failed.Handler is null ? FailedEventStage.Publish : FailedEventStage.Handle,
            failed.Handler,
            failed.Error,
            failed.Attempts,
            failed.Message.AggregateType,
            failed.Message.AggregateId,
            failed.Message.CorrelationId,
            failed.Message.RecordedAt,
            failed.FailedAt);
}
