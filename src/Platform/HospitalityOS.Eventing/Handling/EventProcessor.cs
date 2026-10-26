using HospitalityOS.Contracts.Events;
using HospitalityOS.Eventing.Serialization;
using HospitalityOS.Ports.Eventing;
using HospitalityOS.Ports.Persistence;
using HospitalityOS.SharedKernel.Execution;
using HospitalityOS.SharedKernel.Results;
using Microsoft.Extensions.Logging;

namespace HospitalityOS.Eventing.Handling;

/// <summary>
/// Processes one delivery for one handler, idempotently (P8): skips it if the inbox says it was processed; otherwise
/// runs the handler in a fresh scope for the event's tenant and commits its changes together with the inbox record.
/// If a concurrent delivery won the race, the resulting conflict means "already done". Any other failure is thrown
/// so the transport redelivers and eventually dead-letters. Singleton.
/// </summary>
internal sealed partial class EventProcessor(
    Func<string, HandlerScope> createScope,
    EventSerializer serializer,
    TimeProvider timeProvider,
    ILogger<EventProcessor> logger)
{
    public async Task<HandlingOutcome> ProcessAsync(EventHandlerRegistration registration, EventMessage message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(registration);
        ArgumentNullException.ThrowIfNull(message);

        if (message.EventType != registration.Event.EventType || message.EventVersion != registration.Event.Version)
        {
            return HandlingOutcome.NotHandled;
        }

        await using HandlerScope scope = createScope(registration.Name);

        if (await scope.Inbox.HasProcessedAsync(registration.Name, message.EventId, cancellationToken).ConfigureAwait(false))
        {
            LogDuplicate(registration.Name, message.EventId, message.CorrelationId);

            return HandlingOutcome.Duplicate;
        }

        EventEnvelope envelope = serializer.Deserialize(message.Envelope);
        scope.Tenant.Set(envelope.TenantId);
        scope.Operation.JoinFlow(envelope.CorrelationId, envelope.EventId);
        scope.Operation.ActAs(Actor.Agent(registration.Name));

        using IDisposable? logScope = logger.BeginScope(new Dictionary<string, object>
        {
            ["CorrelationId"] = envelope.CorrelationId,
            ["CausationId"] = envelope.EventId,
            ["Handler"] = registration.Name,
        });

        await scope.Handler.InvokeAsync(envelope, cancellationToken).ConfigureAwait(false);
        scope.Inbox.Record(registration.Name, message.EventId, timeProvider.GetUtcNow());
        Result<CommitReceipt> commit = await scope.UnitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);

        if (commit.IsSuccess)
        {
            LogHandled(registration.Name, message.EventId, message.EventType, message.CorrelationId);

            return HandlingOutcome.Handled;
        }

        // A racing delivery that committed first makes ours conflict, on the inbox key or on an aggregate both changed.
        if (commit.Error.Kind == ErrorKind.Conflict && await ProcessedByAnotherDeliveryAsync(registration, message, cancellationToken).ConfigureAwait(false))
        {
            LogDuplicate(registration.Name, message.EventId, message.CorrelationId);

            return HandlingOutcome.Duplicate;
        }

        // E.g. a concurrency conflict on an aggregate the handler changed: redelivery reloads and tries again.
        throw new EventHandlingException(registration.Name, message.EventId, commit.Error);
    }

    private async Task<bool> ProcessedByAnotherDeliveryAsync(EventHandlerRegistration registration, EventMessage message, CancellationToken cancellationToken)
    {
        await using HandlerScope fresh = createScope(registration.Name);

        return await fresh.Inbox.HasProcessedAsync(registration.Name, message.EventId, cancellationToken).ConfigureAwait(false);
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "{Handler} handled {EventId} ({EventType}), correlation {CorrelationId}")]
    private partial void LogHandled(string handler, Guid eventId, string eventType, Guid correlationId);

    [LoggerMessage(Level = LogLevel.Information, Message = "{Handler} skipped {EventId}, correlation {CorrelationId}: already processed")]
    private partial void LogDuplicate(string handler, Guid eventId, Guid correlationId);
}
