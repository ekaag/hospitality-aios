using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Contracts.Events.Guests;

/// <summary>
/// Version 1 of <c>GuestNotificationRequested</c>: a message to a guest was requested, e.g. that their request is done.
/// The text is not published.
/// </summary>
/// <param name="GuestNotificationId">The notification.</param>
/// <param name="PropertyId">The property sending it.</param>
/// <param name="ConversationId">The guest's conversation.</param>
/// <param name="GuestId">The guest, when known.</param>
/// <param name="Channel">The channel (domain enum name).</param>
/// <param name="SubjectType">What it is about, e.g. ServiceRequest.</param>
/// <param name="SubjectId">The ID of what it is about.</param>
public sealed record GuestNotificationRequestedV1(
    Guid GuestNotificationId,
    PropertyId PropertyId,
    ConversationId ConversationId,
    GuestId? GuestId,
    string Channel,
    string? SubjectType,
    Guid? SubjectId) : IEventPayload;
