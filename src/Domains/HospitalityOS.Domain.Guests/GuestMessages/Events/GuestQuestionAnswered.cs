using HospitalityOS.SharedKernel.Domain;
using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Domain.Guests.GuestMessages.Events;

/// <summary>
/// The concierge answered a guest's question (spec §8, Journey 5); raised after the answer message is created, with it.
/// </summary>
public sealed record GuestQuestionAnswered(
    GuestMessageId QuestionMessageId,
    GuestMessageId AnswerMessageId,
    ConversationId ConversationId,
    PropertyId PropertyId,
    AnswerOutcome Outcome,
    DateTimeOffset OccurredAt) : IDomainEvent;
