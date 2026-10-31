using HospitalityOS.Domain.Guests.Conversations;
using HospitalityOS.Domain.Guests.Notifications.Events;
using HospitalityOS.SharedKernel.Domain;
using HospitalityOS.SharedKernel.Ids;
using HospitalityOS.SharedKernel.Results;
using HospitalityOS.SharedKernel.Tenancy;
using HospitalityOS.SharedKernel.Validation;

namespace HospitalityOS.Domain.Guests.Notifications;

/// <summary>
/// A message the platform sends a guest on its own initiative (spec §8, Epic 11), e.g. that their request is done, sent
/// over the conversation's channel through the property's messaging connector. Its key names one logical message, so a
/// guest is told once however often the cause is handled (Journey 19, P8).
/// </summary>
public sealed class GuestNotification : AggregateRoot<GuestNotificationId>, ITenantScoped, IPropertyScoped
{
    /// <summary>
    /// Maximum length of <see cref="Key"/>.
    /// </summary>
    public const int MaxKeyLength = 200;

    // For EF Core only: it sets every property from storage before the instance is used, so the
    // placeholders below are never read.
    private GuestNotification()
    {
        Key = null!;
        Body = null!;
    }

    private GuestNotification(GuestNotificationId id, Conversation conversation, string key, string body, string? subjectType, Guid? subjectId, DateTimeOffset now)
        : base(id)
    {
        TenantId = conversation.TenantId;
        PropertyId = conversation.PropertyId;
        ConversationId = conversation.Id;
        GuestId = conversation.GuestId;
        Channel = conversation.Channel;
        Key = key;
        Body = body;
        SubjectType = subjectType;
        SubjectId = subjectId;
        RequestedAt = now;
        Status = GuestNotificationStatus.Requested;
    }

    /// <inheritdoc />
    public TenantId TenantId { get; }

    /// <summary>
    /// The property sending it.
    /// </summary>
    public PropertyId PropertyId { get; }

    /// <summary>
    /// The guest's conversation, whose channel it is delivered over.
    /// </summary>
    public ConversationId ConversationId { get; }

    /// <summary>
    /// The guest, when the conversation is linked to one.
    /// </summary>
    public GuestId? GuestId { get; }

    /// <summary>
    /// The channel it is delivered over.
    /// </summary>
    public ConversationChannel Channel { get; }

    /// <summary>
    /// The logical message, e.g. <c>service-request-completed:{id}</c>; unique.
    /// </summary>
    public string Key { get; }

    /// <summary>
    /// The text.
    /// </summary>
    public string Body { get; }

    /// <summary>
    /// What it is about, e.g. <c>ServiceRequest</c>.
    /// </summary>
    public string? SubjectType { get; }

    /// <summary>
    /// The ID of what it is about.
    /// </summary>
    public Guid? SubjectId { get; }

    /// <summary>
    /// When it was requested (UTC).
    /// </summary>
    public DateTimeOffset RequestedAt { get; }

    /// <summary>
    /// Where it is.
    /// </summary>
    public GuestNotificationStatus Status { get; private set; }

    /// <summary>
    /// The messaging provider that accepted it.
    /// </summary>
    public string? ProviderName { get; private set; }

    /// <summary>
    /// The provider's ID for it.
    /// </summary>
    public string? ProviderMessageId { get; private set; }

    /// <summary>
    /// When the provider accepted it (UTC).
    /// </summary>
    public DateTimeOffset? SentAt { get; private set; }

    /// <summary>
    /// Why it could not be sent.
    /// </summary>
    public string? FailureReason { get; private set; }

    /// <summary>
    /// Requests a message to the guest in <paramref name="conversation"/> and raises <see cref="GuestNotificationRequested"/>.
    /// </summary>
    /// <returns>The notification, or a <see cref="ValidationError"/> listing every problem.</returns>
    public static Result<GuestNotification> Request(
        GuestNotificationId id,
        Conversation conversation,
        string key,
        string body,
        string? subjectType,
        Guid? subjectId,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(conversation);

        List<FieldError> errors = [];
        string trimmedKey = TextRules.Required(key, "key", "notification.key", errors, MaxKeyLength);
        string text = TextRules.Required(body, "body", "notification.body", errors, TextRules.MaxLongTextLength);
        string? subject = TextRules.Optional(subjectType, "subjectType", "notification.subject_type", errors, 100);

        if (errors.Count > 0)
        {
            return ValidationError.From(errors);
        }

        GuestNotification notification = new(id, conversation, trimmedKey, text, subject, subjectId, now);
        notification.Raise(new GuestNotificationRequested(
            id, notification.PropertyId, conversation.Id, conversation.GuestId, conversation.Channel, subject, subjectId, now));

        return notification;
    }

    /// <summary>
    /// Records that the provider accepted it and raises <see cref="GuestNotificationSent"/>; sending again is a no-op.
    /// </summary>
    /// <returns>Success, or an <see cref="ErrorKind.InvalidState"/> error when it failed.</returns>
    public Result MarkSent(string providerName, string providerMessageId, DateTimeOffset at)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerName);
        ArgumentException.ThrowIfNullOrWhiteSpace(providerMessageId);

        if (Status == GuestNotificationStatus.Sent)
        {
            return Result.Success();
        }

        if (Status != GuestNotificationStatus.Requested)
        {
            return Error.InvalidState("notification.final", $"A {Status} notification cannot be sent.");
        }

        Status = GuestNotificationStatus.Sent;
        ProviderName = providerName;
        ProviderMessageId = providerMessageId;
        SentAt = at;
        Raise(new GuestNotificationSent(Id, ConversationId, providerName, providerMessageId, at));

        return Result.Success();
    }

    /// <summary>
    /// Records that it could not be sent and raises <see cref="GuestNotificationFailed"/>; failing again is a no-op.
    /// </summary>
    /// <returns>Success, or an <see cref="ErrorKind.InvalidState"/> error when it was sent.</returns>
    public Result MarkFailed(string reason, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        if (Status == GuestNotificationStatus.Failed)
        {
            return Result.Success();
        }

        if (Status != GuestNotificationStatus.Requested)
        {
            return Error.InvalidState("notification.final", $"A {Status} notification cannot fail.");
        }

        Status = GuestNotificationStatus.Failed;
        FailureReason = reason.Trim();
        Raise(new GuestNotificationFailed(Id, ConversationId, FailureReason, now));

        return Result.Success();
    }
}
