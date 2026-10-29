using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Contracts.Events.Guests;

/// <summary>
/// Version 1 of <c>GuestQuestionAnswered</c>: the concierge answered a guest's question. Carries IDs only, never the text (P10);
/// the turn's AI trace has the same correlation ID and the question as its trigger.
/// </summary>
/// <param name="QuestionMessageId">The guest's message.</param>
/// <param name="AnswerMessageId">The concierge's answer.</param>
/// <param name="ConversationId">The conversation.</param>
/// <param name="PropertyId">The property.</param>
/// <param name="Outcome">Whether an authoritative source answered it (domain enum name): <c>Answered</c> or
/// <c>NoAuthoritativeSource</c>.</param>
public sealed record GuestQuestionAnsweredV1(
    GuestMessageId QuestionMessageId,
    GuestMessageId AnswerMessageId,
    ConversationId ConversationId,
    PropertyId PropertyId,
    string Outcome) : IEventPayload;
