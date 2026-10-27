namespace HospitalityOS.IntegrationContracts.Messaging;

/// <summary>
/// A message to one guest.
/// </summary>
/// <param name="IdempotencyKey">Identifies the logical message, e.g. derived from the notification's ID, so a retried or
/// duplicated send reaches the guest once (P8).</param>
/// <param name="Recipient">Where to deliver it.</param>
/// <param name="Body">The text, in the guest's language.</param>
public sealed record OutboundMessage(string IdempotencyKey, MessageRecipient Recipient, string Body);
