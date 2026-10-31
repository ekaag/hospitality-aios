using HospitalityOS.Domain.Guests.Conversations;
using HospitalityOS.Domain.Guests.GuestMessages.Events;
using HospitalityOS.SharedKernel.Domain;
using HospitalityOS.SharedKernel.Ids;
using HospitalityOS.SharedKernel.Results;
using HospitalityOS.SharedKernel.Tenancy;
using HospitalityOS.SharedKernel.Validation;

namespace HospitalityOS.Domain.Guests.GuestMessages;

/// <summary>
/// One immutable message in a <see cref="Conversation"/>. A guest's message raises
/// <see cref="GuestMessageReceived"/>, which starts the concierge and guest-service workflows (spec §8, P1).
/// </summary>
/// <remarks>
/// Text may contain personal data; AI traces redact it by policy (P11). A channel may deliver the same guest
/// message twice, so callers pass the channel's message ID as <see cref="ClientMessageId"/> and check
/// <see cref="IGuestMessageRepository.FindByClientMessageIdAsync"/> first (P8).
/// </remarks>
public sealed class GuestMessage : AggregateRoot<GuestMessageId>, ITenantScoped, IPropertyScoped
{
    /// <summary>
    /// Maximum length of <see cref="Text"/>.
    /// </summary>
    public const int MaxTextLength = 4000;

    /// <summary>
    /// Maximum length of <see cref="ClientMessageId"/>.
    /// </summary>
    public const int MaxClientMessageIdLength = 100;

    // For EF Core only: it sets every property from storage before the instance is used, so the
    // placeholders below are never read.
    private GuestMessage()
    {
        Text = null!;
    }

    private GuestMessage(
        GuestMessageId id,
        Conversation conversation,
        MessageSender sender,
        string text,
        string? clientMessageId,
        DateTimeOffset sentAt)
        : base(id)
    {
        TenantId = conversation.TenantId;
        PropertyId = conversation.PropertyId;
        ConversationId = conversation.Id;
        GuestId = conversation.GuestId;
        StayContextId = conversation.StayContextId;
        Sender = sender;
        Text = text;
        ClientMessageId = clientMessageId;
        SentAt = sentAt;
    }

    /// <inheritdoc />
    public TenantId TenantId { get; }

    /// <summary>
    /// The property of the conversation.
    /// </summary>
    public PropertyId PropertyId { get; }

    /// <summary>
    /// The conversation the message belongs to.
    /// </summary>
    public ConversationId ConversationId { get; }

    /// <summary>
    /// The guest, if the conversation was linked when the message was recorded.
    /// </summary>
    public GuestId? GuestId { get; }

    /// <summary>
    /// The stay, if the conversation was linked when the message was recorded.
    /// </summary>
    public StayContextId? StayContextId { get; }

    /// <summary>
    /// Who wrote it.
    /// </summary>
    public MessageSender Sender { get; }

    /// <summary>
    /// The message text, trimmed.
    /// </summary>
    public string Text { get; }

    /// <summary>
    /// The channel's own ID for the message, used to ignore redelivered duplicates; optional.
    /// </summary>
    public string? ClientMessageId { get; }

    /// <summary>
    /// When it was recorded (UTC).
    /// </summary>
    public DateTimeOffset SentAt { get; }

    /// <summary>
    /// What a guest's message asks for, once triaged; the message's only change after it is recorded.
    /// </summary>
    public MessageIntent? Intent { get; private set; }

    /// <summary>
    /// Records what a guest's message asks for and raises <see cref="GuestMessageTriaged"/>, which routes it to the
    /// controller that handles it. Triaging again with the same intent is a no-op; a message is triaged once.
    /// </summary>
    /// <param name="intent">What the message asks for.</param>
    /// <param name="now">Current time from the injected <see cref="TimeProvider"/>.</param>
    /// <returns>Success, a <see cref="ValidationError"/>, or an <see cref="ErrorKind.InvalidState"/> error when the message is
    /// not a guest's or was triaged differently.</returns>
    public Result Triage(MessageIntent intent, DateTimeOffset now)
    {
        if (!Enum.IsDefined(intent))
        {
            return Error.Validation("intent", "message.intent_invalid", $"'{intent}' is not a known intent.");
        }

        if (Sender != MessageSender.Guest)
        {
            return Error.InvalidState("message.not_from_guest", "Only a guest's message is triaged.");
        }

        if (Intent == intent)
        {
            return Result.Success();
        }

        if (Intent is not null)
        {
            return Error.InvalidState("message.already_triaged", $"The message was already triaged as {Intent}.");
        }

        Intent = intent;
        Raise(new GuestMessageTriaged(Id, ConversationId, PropertyId, GuestId, StayContextId, intent, now));

        return Result.Success();
    }

    /// <summary>
    /// Records a message in an open conversation. Raises <see cref="GuestMessageReceived"/> for guest messages
    /// and <see cref="GuestMessageSent"/> for everyone else's.
    /// </summary>
    /// <param name="id">New ID, generated by the caller through <c>IIdGenerator</c>.</param>
    /// <param name="conversation">The conversation, loaded by the caller; must be open.</param>
    /// <param name="sender">Who wrote it.</param>
    /// <param name="text">The text; required.</param>
    /// <param name="clientMessageId">The channel's message ID for de-duplication; optional.</param>
    /// <param name="now">Current time from the injected <see cref="TimeProvider"/>.</param>
    /// <returns>The message, a <see cref="ValidationError"/>, or an <see cref="ErrorKind.InvalidState"/> error when closed.</returns>
    public static Result<GuestMessage> Record(
        GuestMessageId id,
        Conversation conversation,
        MessageSender sender,
        string text,
        string? clientMessageId,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(conversation);

        if (conversation.Status == ConversationStatus.Closed)
        {
            return Conversation.ClosedError();
        }

        List<FieldError> errors = [];
        string trimmed = TextRules.Required(text, "text", "message.text", errors, MaxTextLength);
        string? clientId = TextRules.Optional(clientMessageId, "clientMessageId", "message.client_message_id", errors, MaxClientMessageIdLength);

        if (!Enum.IsDefined(sender))
        {
            errors.Add(new FieldError("sender", "message.sender_invalid", $"'{sender}' is not a known sender."));
        }

        if (errors.Count > 0)
        {
            return ValidationError.From(errors);
        }

        GuestMessage message = new(id, conversation, sender, trimmed, clientId, now);

        message.Raise(sender == MessageSender.Guest
            ? new GuestMessageReceived(id, message.ConversationId, message.PropertyId, message.GuestId, message.StayContextId, now)
            : new GuestMessageSent(id, message.ConversationId, message.PropertyId, sender, now));

        return message;
    }

    /// <summary>
    /// Records the concierge's answer to a guest's question. Raises <see cref="GuestMessageSent"/>, then
    /// <see cref="GuestQuestionAnswered"/>.
    /// </summary>
    /// <param name="id">New ID, generated by the caller through <c>IIdGenerator</c>.</param>
    /// <param name="conversation">The question's conversation, loaded by the caller; must be open.</param>
    /// <param name="question">The guest's message being answered.</param>
    /// <param name="text">What the guest is told; required.</param>
    /// <param name="outcome">Whether an authoritative source answered the question.</param>
    /// <param name="now">Current time from the injected <see cref="TimeProvider"/>.</param>
    /// <returns>The answer, a <see cref="ValidationError"/>, or an <see cref="ErrorKind.InvalidState"/> error when closed.</returns>
    /// <exception cref="ArgumentException">The question is not a guest's message in <paramref name="conversation"/>.</exception>
    public static Result<GuestMessage> RecordAnswer(
        GuestMessageId id,
        Conversation conversation,
        GuestMessage question,
        string text,
        AnswerOutcome outcome,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(conversation);
        ArgumentNullException.ThrowIfNull(question);

        if (question.Sender != MessageSender.Guest || question.ConversationId != conversation.Id)
        {
            throw new ArgumentException("Only a guest's message in the same conversation can be answered.", nameof(question));
        }

        if (!Enum.IsDefined(outcome))
        {
            return Error.Validation("outcome", "message.outcome_invalid", $"'{outcome}' is not a known answer outcome.");
        }

        Result<GuestMessage> answer = Record(id, conversation, MessageSender.Concierge, text, null, now);

        if (answer.IsSuccess)
        {
            answer.Value.Raise(new GuestQuestionAnswered(question.Id, id, conversation.Id, conversation.PropertyId, outcome, now));
        }

        return answer;
    }
}
