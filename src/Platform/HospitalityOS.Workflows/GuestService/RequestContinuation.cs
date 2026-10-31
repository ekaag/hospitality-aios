using HospitalityOS.Contracts.Events;
using HospitalityOS.Contracts.Events.Operations;
using HospitalityOS.Domain.Operations.HumanActions;
using HospitalityOS.Domain.Services.ServiceRequests;
using HospitalityOS.Eventing.Handling;
using HospitalityOS.SharedKernel.Ids;
using HospitalityOS.SharedKernel.Results;
using Microsoft.Extensions.Logging;

namespace HospitalityOS.Workflows.GuestService;

/// <summary>
/// The Guest Service Controller resuming on the staff work it asked for (spec §9, P2): no manager moves a request on.
/// Work started puts the request in progress; work completed completes it once no other work for it is open, raising
/// <c>ServiceRequestCompleted</c> exactly once however often the event is delivered (Journeys 14, 19); work failed, or
/// cancelled or expired while the request still needs it, keeps the request unresolved and puts it into exception with
/// why, which is the managers' exceptions queue (Journey 20).
/// </summary>
/// <remarks>
/// Each event is handled under its own name and commits with its inbox record (P8); the request's own state machine makes
/// a repeat harmless besides. Work for no request, or for a request already resolved, changes nothing.
/// </remarks>
internal sealed partial class RequestContinuation(
    IServiceRequestRepository requests,
    IHumanActionRepository actions,
    TimeProvider timeProvider,
    ILogger<RequestContinuation> logger)
    : IEventHandler<HumanActionStartedV1>,
        IEventHandler<HumanActionCompletedV1>,
        IEventHandler<HumanActionFailedV1>,
        IEventHandler<HumanActionCancelledV1>,
        IEventHandler<HumanActionExpiredV1>
{
    public const string OnStarted = "guestservice.on_work_started";
    public const string OnCompleted = "guestservice.on_work_completed";
    public const string OnFailed = "guestservice.on_work_failed";
    public const string OnCancelled = "guestservice.on_work_cancelled";
    public const string OnExpired = "guestservice.on_work_expired";

    public Task HandleAsync(EventEnvelope envelope, HumanActionStartedV1 payload, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(payload);

        return ContinueAsync(payload.ServiceRequestId, null, request => request.MarkInProgress(timeProvider.GetUtcNow()), cancellationToken);
    }

    public Task HandleAsync(EventEnvelope envelope, HumanActionCompletedV1 payload, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(payload);

        return ContinueAsync(payload.ServiceRequestId, payload.HumanActionId, request => request.Complete(timeProvider.GetUtcNow()), cancellationToken);
    }

    public Task HandleAsync(EventEnvelope envelope, HumanActionFailedV1 payload, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(payload);

        return ContinueAsync(
            payload.ServiceRequestId,
            payload.HumanActionId,
            request => request.RecordException($"The work failed: {payload.Reason}", timeProvider.GetUtcNow()),
            cancellationToken);
    }

    public Task HandleAsync(EventEnvelope envelope, HumanActionCancelledV1 payload, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(payload);

        return ContinueAsync(
            payload.ServiceRequestId,
            payload.HumanActionId,
            request => request.RecordException($"The work was cancelled: {payload.Reason}", timeProvider.GetUtcNow()),
            cancellationToken);
    }

    public Task HandleAsync(EventEnvelope envelope, HumanActionExpiredV1 payload, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(payload);

        return ContinueAsync(
            payload.ServiceRequestId,
            payload.HumanActionId,
            request => request.RecordException("The work was not started before its deadline.", timeProvider.GetUtcNow()),
            cancellationToken);
    }

    /// <summary>
    /// Applies <paramref name="change"/> to the request the work was for. When <paramref name="ended"/> names work that
    /// ended, the change waits for the request's other open work: the request is resolved by the last piece to end.
    /// </summary>
    private async Task ContinueAsync(ServiceRequestId? requestId, Guid? ended, Func<ServiceRequest, Result> change, CancellationToken cancellationToken)
    {
        if (requestId is not { } id
            || await requests.GetByIdAsync(id, cancellationToken).ConfigureAwait(false) is not { IsActive: true } request)
        {
            return;
        }

        if (ended is { } endedId)
        {
            IReadOnlyList<HumanAction> work = await actions.ListForServiceRequestAsync(id, cancellationToken).ConfigureAwait(false);

            if (work.Any(action => action.Id.Value != endedId && !action.IsFinal))
            {
                return;
            }
        }

        Result changed = change(request);

        // The texts are the controller's own and the request is active, so only an invalid reason could refuse a change.
        if (changed.IsFailure)
        {
            LogRefused(id.Value, changed.Error.Code);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Service request {ServiceRequestId} was not moved on ({ErrorCode})")]
    private partial void LogRefused(Guid serviceRequestId, string errorCode);
}
