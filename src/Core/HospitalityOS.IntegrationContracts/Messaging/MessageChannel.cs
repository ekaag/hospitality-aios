namespace HospitalityOS.IntegrationContracts.Messaging;

/// <summary>
/// The channel a message is delivered over, named as the canonical conversation channel.
/// </summary>
public enum MessageChannel
{
    /// <summary>
    /// The guest web experience.
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
