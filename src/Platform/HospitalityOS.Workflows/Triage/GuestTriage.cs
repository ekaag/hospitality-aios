using HospitalityOS.Ai.Classifying;
using HospitalityOS.Ai.Tracing;
using HospitalityOS.Contracts.Events;
using HospitalityOS.Contracts.Events.Guests;
using HospitalityOS.Domain.Capabilities.AITraces;
using HospitalityOS.Domain.Guests.GuestMessages;
using HospitalityOS.Eventing.Handling;
using HospitalityOS.SharedKernel.Results;
using Microsoft.Extensions.Logging;

namespace HospitalityOS.Workflows.Triage;

/// <summary>
/// Triages each guest message once, by its intent, so exactly one controller handles it: questions and status checks go
/// to the concierge, physical requests to the Guest Service Controller. The intent is recorded on the message, which
/// raises <c>GuestMessageTriaged</c>, together with the turn's AI trace (outcome <c>Routed</c>). When the model cannot
/// classify it, the message goes to the concierge as a question, which then answers or says it cannot right now; the trace
/// records the failure.
/// </summary>
internal sealed partial class GuestTriage(
    IGuestMessageRepository messages,
    ILabelClassifier classifier,
    IAITraceRecorder traces,
    TimeProvider timeProvider,
    ILogger<GuestTriage> logger) : IEventHandler<GuestMessageReceivedV1>
{
    /// <summary>
    /// The classification task, and the handler's name, so its deliveries act as that agent.
    /// </summary>
    public const string Task = "intent.classify";

    public const string QuestionLabel = "question";
    public const string ServiceRequestLabel = "service_request";
    public const string RequestStatusLabel = "request_status";

    private static readonly string[] labels = [QuestionLabel, ServiceRequestLabel, RequestStatusLabel];

    public async Task HandleAsync(EventEnvelope envelope, GuestMessageReceivedV1 payload, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(payload);

        // Committed with the event, so it cannot be missing.
        GuestMessage message = await messages.GetByIdAsync(payload.GuestMessageId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Guest message {payload.GuestMessageId} does not exist.");

        if (message.Intent is not null)
        {
            return;
        }

        Result<string> label = await classifier.ClassifyAsync(Task, message.Text, labels, cancellationToken).ConfigureAwait(false);
        MessageIntent intent = label.IsSuccess ? Intent(label.Value) : MessageIntent.Question;

        if (label.IsFailure)
        {
            LogUnclassified(message.Id.Value, label.Error.Code);
        }

        Result triaged = message.Triage(intent, timeProvider.GetUtcNow());

        if (triaged.IsFailure)
        {
            throw new InvalidOperationException($"Guest message {message.Id} could not be triaged: {triaged.Error.Message}");
        }

        await traces.CompleteAsync(
            new AITraceCompletion(
                new AITraceTrigger(AITraceTrigger.GuestMessage, message.Id.Value, message.Text),
                label.IsSuccess ? AITraceOutcome.Routed : AITraceOutcome.Failed,
                null,
                [],
                message.PropertyId,
                label.IsSuccess ? label.Value : null),
            cancellationToken).ConfigureAwait(false);
    }

    private static MessageIntent Intent(string label) => label switch
    {
        ServiceRequestLabel => MessageIntent.ServiceRequest,
        RequestStatusLabel => MessageIntent.RequestStatus,
        _ => MessageIntent.Question,
    };

    [LoggerMessage(Level = LogLevel.Warning, Message = "Guest message {MessageId} could not be classified ({ErrorCode}); it goes to the concierge as a question")]
    private partial void LogUnclassified(Guid messageId, string errorCode);
}
