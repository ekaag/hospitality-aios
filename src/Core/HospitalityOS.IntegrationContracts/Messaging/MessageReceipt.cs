namespace HospitalityOS.IntegrationContracts.Messaging;

/// <summary>
/// The provider's acknowledgement that it accepted a message for delivery.
/// </summary>
/// <param name="MessageId">The provider's ID for the message.</param>
/// <param name="AcceptedAt">When the provider first accepted it.</param>
public sealed record MessageReceipt(ExternalId MessageId, DateTimeOffset AcceptedAt);
