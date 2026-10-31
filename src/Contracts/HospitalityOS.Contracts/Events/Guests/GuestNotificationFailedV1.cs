using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Contracts.Events.Guests;

/// <summary>
/// Version 1 of <c>GuestNotificationFailed</c>: a message to a guest could not be sent.
/// </summary>
/// <param name="GuestNotificationId">The notification.</param>
/// <param name="ConversationId">The guest's conversation.</param>
/// <param name="Reason">Why, e.g. the provider refused the recipient.</param>
public sealed record GuestNotificationFailedV1(
    Guid GuestNotificationId,
    ConversationId ConversationId,
    string Reason) : IEventPayload;
