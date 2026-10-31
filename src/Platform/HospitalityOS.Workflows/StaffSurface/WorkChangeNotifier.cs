using System.Text.Json;
using HospitalityOS.Contracts.Events;
using HospitalityOS.Contracts.Events.Operations;
using HospitalityOS.Contracts.Serialization;
using HospitalityOS.Eventing.Handling;
using HospitalityOS.Ports.Bindings;
using HospitalityOS.Ports.Realtime;

namespace HospitalityOS.Workflows.StaffSurface;

/// <summary>
/// Tells the staff surface that work changed (J2, the plan's <c>IRealtimeNotifier</c>): every human action event is
/// pushed to its tenant's staff channel as <see cref="MessageType"/>, naming the work and what happened, never what the
/// work is, so a client reads it again through the API, which decides what its caller may see (P10). A push is best
/// effort: a client that misses one still reads the work when it next loads it.
/// </summary>
internal sealed class WorkChangeNotifier(IBindingResolver<IRealtimeNotifier> notifiers)
    : IEventHandler<HumanActionRequestedV1>,
        IEventHandler<HumanActionAssignedV1>,
        IEventHandler<HumanActionAcceptedV1>,
        IEventHandler<HumanActionStartedV1>,
        IEventHandler<HumanActionCompletedV1>,
        IEventHandler<HumanActionFailedV1>,
        IEventHandler<HumanActionCancelledV1>,
        IEventHandler<HumanActionExpiredV1>
{
    public const string OnRequested = "staffsurface.on_work_requested";
    public const string OnAssigned = "staffsurface.on_work_assigned";
    public const string OnAccepted = "staffsurface.on_work_accepted";
    public const string OnStarted = "staffsurface.on_work_started";
    public const string OnCompleted = "staffsurface.on_work_completed";
    public const string OnFailed = "staffsurface.on_work_failed";
    public const string OnCancelled = "staffsurface.on_work_cancelled";
    public const string OnExpired = "staffsurface.on_work_expired";

    /// <summary>
    /// The message's type, which clients subscribe to.
    /// </summary>
    public const string MessageType = "WorkChanged";

    public Task HandleAsync(EventEnvelope envelope, HumanActionRequestedV1 payload, CancellationToken cancellationToken) =>
        PublishAsync(envelope, payload, payload => payload.HumanActionId, cancellationToken);

    public Task HandleAsync(EventEnvelope envelope, HumanActionAssignedV1 payload, CancellationToken cancellationToken) =>
        PublishAsync(envelope, payload, payload => payload.HumanActionId, cancellationToken);

    public Task HandleAsync(EventEnvelope envelope, HumanActionAcceptedV1 payload, CancellationToken cancellationToken) =>
        PublishAsync(envelope, payload, payload => payload.HumanActionId, cancellationToken);

    public Task HandleAsync(EventEnvelope envelope, HumanActionStartedV1 payload, CancellationToken cancellationToken) =>
        PublishAsync(envelope, payload, payload => payload.HumanActionId, cancellationToken);

    public Task HandleAsync(EventEnvelope envelope, HumanActionCompletedV1 payload, CancellationToken cancellationToken) =>
        PublishAsync(envelope, payload, payload => payload.HumanActionId, cancellationToken);

    public Task HandleAsync(EventEnvelope envelope, HumanActionFailedV1 payload, CancellationToken cancellationToken) =>
        PublishAsync(envelope, payload, payload => payload.HumanActionId, cancellationToken);

    public Task HandleAsync(EventEnvelope envelope, HumanActionCancelledV1 payload, CancellationToken cancellationToken) =>
        PublishAsync(envelope, payload, payload => payload.HumanActionId, cancellationToken);

    public Task HandleAsync(EventEnvelope envelope, HumanActionExpiredV1 payload, CancellationToken cancellationToken) =>
        PublishAsync(envelope, payload, payload => payload.HumanActionId, cancellationToken);

    private Task PublishAsync<TPayload>(EventEnvelope envelope, TPayload payload, Func<TPayload, Guid> humanActionId, CancellationToken cancellationToken)
        where TPayload : class
    {
        ArgumentNullException.ThrowIfNull(envelope);
        ArgumentNullException.ThrowIfNull(payload);

        return notifiers.Resolve().PublishAsync(
            new RealtimeMessage(
                StaffChannel.For(envelope.TenantId),
                MessageType,
                JsonSerializer.SerializeToElement(new WorkChanged(humanActionId(payload), envelope.EventType), ContractJson.Options)),
            cancellationToken);
    }

    /// <summary>
    /// What a <see cref="MessageType"/> message carries.
    /// </summary>
    /// <param name="HumanActionId">The work that changed.</param>
    /// <param name="Change">The event, e.g. <c>HumanActionAssigned</c>.</param>
    private sealed record WorkChanged(Guid HumanActionId, string Change);
}
