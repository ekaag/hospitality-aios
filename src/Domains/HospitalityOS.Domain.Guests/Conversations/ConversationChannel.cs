namespace HospitalityOS.Domain.Guests.Conversations;

/// <summary>
/// Where a <see cref="Conversation"/> takes place. Delivery goes through <c>IMessagingConnector</c>; the
/// channel only records origin.
/// </summary>
public enum ConversationChannel
{
    /// <summary>
    /// The guest web experience (Phase 1's primary surface).
    /// </summary>
    Web,

    /// <summary>
    /// Text messages.
    /// </summary>
    Sms,

    /// <summary>
    /// A chat app such as WhatsApp.
    /// </summary>
    ChatApp,

    /// <summary>
    /// Anything else.
    /// </summary>
    Other,
}
