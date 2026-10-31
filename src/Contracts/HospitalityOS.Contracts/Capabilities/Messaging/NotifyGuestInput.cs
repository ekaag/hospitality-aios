using System.ComponentModel;
using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Contracts.Capabilities.Messaging;

/// <summary>
/// Input of <c>Messaging.NotifyGuest</c>.
/// </summary>
public sealed record NotifyGuestInput(
    [property: Description("The guest's conversation; the message goes over its channel.")] ConversationId ConversationId,
    [property: Description("The text, in the guest's language.")] string Body,
    [property: Description("Names the logical message, e.g. service-request-completed:{id}; a key is sent at most once.")] string Key,
    [property: Description("What it is about, e.g. ServiceRequest.")] string? SubjectType = null,
    [property: Description("The ID of what it is about.")] Guid? SubjectId = null);
