namespace HospitalityOS.IntegrationContracts.Messaging;

/// <summary>
/// Where a message is delivered. The address is personal data: never log it (P10).
/// </summary>
/// <param name="Channel">The channel to deliver over.</param>
/// <param name="Address">The recipient in the channel's own terms: the conversation ID for <see cref="MessageChannel.Web"/>,
/// a phone number for <see cref="MessageChannel.Sms"/>, and so on.</param>
public sealed record MessageRecipient(MessageChannel Channel, string Address);
