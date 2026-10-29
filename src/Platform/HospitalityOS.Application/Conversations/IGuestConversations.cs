using HospitalityOS.Contracts.Api.Conversations;
using HospitalityOS.SharedKernel.Results;

namespace HospitalityOS.Application.Conversations;

/// <summary>
/// A guest's conversations with the property (spec §38 <c>/conversations</c>, Journey 5). Each is linked to the caller's
/// stay when it starts, and a guest reaches only the conversations of their own stay. A guest's message raises
/// <c>GuestMessageReceived</c>, which the concierge answers asynchronously with a message of its own. Every call needs the
/// caller's stay to be active (reserved or in house) at the property. Scoped.
/// </summary>
public interface IGuestConversations
{
    /// <summary>
    /// Starts an open web conversation linked to the caller's stay.
    /// </summary>
    /// <returns>The conversation, or <see cref="ConversationErrors.StayNotActive"/>.</returns>
    Task<Result<ConversationResponse>> StartAsync(GuestCaller caller, CancellationToken cancellationToken);

    /// <summary>
    /// One of the caller's conversations.
    /// </summary>
    Task<Result<ConversationResponse>> GetAsync(GuestCaller caller, Guid conversationId, CancellationToken cancellationToken);

    /// <summary>
    /// Records a guest's message, once per client message ID (P8): sending the same ID again returns the recorded message.
    /// </summary>
    /// <returns>The message and whether this call recorded it; field errors; <c>conversation.closed</c>; or the errors of
    /// <see cref="GetAsync"/>.</returns>
    Task<Result<PostedMessage>> PostMessageAsync(GuestCaller caller, Guid conversationId, PostMessageRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// The conversation's most recent messages, oldest first: the guest's and the concierge's answers as they arrive.
    /// </summary>
    Task<Result<IReadOnlyList<GuestMessageResponse>>> ListMessagesAsync(GuestCaller caller, Guid conversationId, CancellationToken cancellationToken);
}
