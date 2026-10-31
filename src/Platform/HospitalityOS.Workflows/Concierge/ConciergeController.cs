using HospitalityOS.Ai.Grounding;
using HospitalityOS.Ai.Tracing;
using HospitalityOS.Contracts.Events;
using HospitalityOS.Contracts.Events.Guests;
using HospitalityOS.Domain.Capabilities.AITraces;
using HospitalityOS.Domain.Guests.Conversations;
using HospitalityOS.Domain.Guests.GuestMessages;
using HospitalityOS.Eventing.Handling;
using HospitalityOS.Ports.Bindings;
using HospitalityOS.Ports.Ids;
using HospitalityOS.Ports.Inference;
using HospitalityOS.SharedKernel.Collections;
using HospitalityOS.SharedKernel.Ids;
using HospitalityOS.SharedKernel.Results;
using Microsoft.Extensions.Logging;

namespace HospitalityOS.Workflows.Concierge;

/// <summary>
/// The Concierge Controller (spec §9, Journey 5): answers each guest message triaged as a question or a status check
/// from the property's authoritative information, and a status check also from the guest's own requests (Journey 12),
/// read for the message's stay, never one the model names. It loads the message and the conversation so far, has <see cref="IGroundedAnswerer"/> answer the last
/// question, and records the answer as the concierge's message, which raises <c>GuestMessageSent</c> and then
/// <c>GuestQuestionAnswered</c>, together with the turn's AI trace. All of it commits with the inbox record, so a
/// redelivered message is answered once (P8). When the model cannot answer at all, the guest is told so honestly and the
/// trace records the failure, without <c>GuestQuestionAnswered</c>. A message in a conversation closed since is left
/// unanswered.
/// </summary>
internal sealed partial class ConciergeController(
    IGuestMessageRepository messages,
    IConversationRepository conversations,
    IGroundedAnswerer answerer,
    IAITraceRecorder traces,
    IBindingResolver<IIdGenerator> idGenerators,
    TimeProvider timeProvider,
    ILogger<ConciergeController> logger) : IEventHandler<GuestMessageTriagedV1>
{
    /// <summary>
    /// The handler's name, and so the agent its deliveries act as: the task's, because grounded answering acts as the
    /// task's agent and an operation has one actor.
    /// </summary>
    public const string OnGuestMessageTriaged = Concierge.AnswerTask;

    /// <summary>
    /// Earlier messages of the conversation the model sees with the question.
    /// </summary>
    public const int HistoryMessages = 20;

    public async Task HandleAsync(EventEnvelope envelope, GuestMessageTriagedV1 payload, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(payload);

        if (payload.Intent == nameof(MessageIntent.ServiceRequest))
        {
            return;
        }

        // Both were committed with the event, so neither can be missing.
        GuestMessage question = await messages.GetByIdAsync(payload.GuestMessageId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Guest message {payload.GuestMessageId} does not exist.");
        Conversation conversation = await conversations.GetByIdAsync(payload.ConversationId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Conversation {payload.ConversationId} does not exist.");

        if (conversation.Status == ConversationStatus.Closed)
        {
            LogClosed(question.Id.Value, conversation.Id.Value);

            return;
        }

        ValueList<ChatMessage> history = await HistoryAsync(question, cancellationToken).ConfigureAwait(false);
        Result<GroundedAnswer> answer = await answerer
            .AnswerAsync(
                new GroundedQuestion(Concierge.AnswerTask, question.PropertyId, history, Concierge.NoSourceAnswer, ExtraContext: ExtraContext(payload)),
                cancellationToken)
            .ConfigureAwait(false);
        AITraceTrigger trigger = new(AITraceTrigger.GuestMessage, question.Id.Value, question.Text);
        string intent = payload.Intent == nameof(MessageIntent.RequestStatus) ? Concierge.StatusIntent : Concierge.QuestionIntent;
        GuestMessageId replyId = new(idGenerators.Resolve().NewId());
        DateTimeOffset now = timeProvider.GetUtcNow();
        Result<GuestMessage> reply;

        if (answer.IsSuccess)
        {
            AnswerOutcome outcome = answer.Value.Outcome switch
            {
                GroundedOutcome.Answered => AnswerOutcome.Answered,
                GroundedOutcome.Uncertain => AnswerOutcome.Uncertain,
                _ => AnswerOutcome.NoAuthoritativeSource,
            };
            reply = GuestMessage.RecordAnswer(replyId, conversation, question, answer.Value.Text, outcome, now);
            await traces.CompleteAsync(AITraceCompletion.For(trigger, question.PropertyId, answer.Value, intent), cancellationToken).ConfigureAwait(false);
        }
        else
        {
            LogUnanswered(question.Id.Value, answer.Error.Code);
            reply = GuestMessage.Record(replyId, conversation, MessageSender.Concierge, Concierge.UnavailableAnswer, null, now);
            await traces.CompleteAsync(
                new AITraceCompletion(trigger, AITraceOutcome.Failed, Concierge.UnavailableAnswer, [], question.PropertyId, intent),
                cancellationToken).ConfigureAwait(false);
        }

        // E.g. an answer longer than a message may be: thrown, so the delivery is retried and eventually dead-lettered.
        if (reply.IsFailure)
        {
            throw new InvalidOperationException($"The concierge's reply to {question.Id} could not be recorded: {reply.Error.Message}");
        }

        await messages.AddAsync(reply.Value, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// For a status check by an identified guest, their unresolved requests, read for the message's stay.
    /// </summary>
    private static ContextReadRequest[] ExtraContext(GuestMessageTriagedV1 payload) =>
        payload.Intent == nameof(MessageIntent.RequestStatus) && payload.StayContextId is { } stay
            ? [new ContextReadRequest(Concierge.RequestsContext, Concierge.RequestStatusCapability, new { stayContextId = stay })]
            : [];

    /// <summary>
    /// The conversation up to and including the question, oldest first: the guest's messages as the user's, everyone
    /// else's as the assistant's. Messages that arrived after the question are answered in their own turn.
    /// </summary>
    private async Task<ValueList<ChatMessage>> HistoryAsync(GuestMessage question, CancellationToken cancellationToken)
    {
        IReadOnlyList<GuestMessage> recent = await messages.ListRecentAsync(question.ConversationId, HistoryMessages + 1, cancellationToken).ConfigureAwait(false);
        int asked = recent.Select(message => message.Id).ToList().IndexOf(question.Id);
        IEnumerable<GuestMessage> upToQuestion = asked < 0 ? [question] : recent.Take(asked + 1);

        return ValueList.From(upToQuestion.Select(message =>
            message.Sender == MessageSender.Guest ? ChatMessage.User(message.Text) : ChatMessage.Assistant(message.Text)));
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Guest message {MessageId} left unanswered: conversation {ConversationId} is closed")]
    private partial void LogClosed(Guid messageId, Guid conversationId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "The concierge could not answer guest message {MessageId} ({ErrorCode}); the guest was told to try again")]
    private partial void LogUnanswered(Guid messageId, string errorCode);
}
