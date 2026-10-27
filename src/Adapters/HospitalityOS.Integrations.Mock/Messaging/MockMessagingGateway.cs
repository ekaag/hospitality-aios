using HospitalityOS.IntegrationContracts;
using HospitalityOS.IntegrationContracts.Messaging;
using HospitalityOS.SharedKernel.Ids;
using HospitalityOS.SharedKernel.Results;
using Microsoft.Extensions.Options;

namespace HospitalityOS.Integrations.Mock.Messaging;

/// <summary>
/// The mock messaging service behind every property's connector: accepts each message once per idempotency key and
/// property (P8), and keeps what it delivered so tests can see what guests received. Singleton; survives connectors being
/// recreated when a configuration changes.
/// </summary>
internal sealed class MockMessagingGateway(IOptions<MockMessagingOptions> options, TimeProvider timeProvider)
{
    private readonly Dictionary<(PropertyId Property, string Key), MessageReceipt> receipts = [];
    private readonly List<DeliveredMessage> delivered = [];
    private readonly Lock gate = new();

    public Result<MessageReceipt> Send(PropertyId propertyId, OutboundMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentException.ThrowIfNullOrWhiteSpace(message.IdempotencyKey);

        lock (gate)
        {
            if (receipts.TryGetValue((propertyId, message.IdempotencyKey), out MessageReceipt? accepted))
            {
                return accepted;
            }

            if (string.IsNullOrWhiteSpace(message.Recipient.Address) || options.Value.UnreachableAddresses.Contains(message.Recipient.Address))
            {
                return IntegrationErrors.Rejected($"The recipient cannot be reached by {message.Recipient.Channel}.");
            }

            if (string.IsNullOrWhiteSpace(message.Body))
            {
                return IntegrationErrors.Rejected("The message has no text.");
            }

            MessageReceipt receipt = new(new ExternalId($"mock-msg-{delivered.Count + 1:D6}"), timeProvider.GetUtcNow());
            receipts.Add((propertyId, message.IdempotencyKey), receipt);
            delivered.Add(new DeliveredMessage(propertyId, message, receipt));

            return receipt;
        }
    }

    /// <summary>
    /// What was delivered to a property's guests, oldest first.
    /// </summary>
    public IReadOnlyList<DeliveredMessage> DeliveredFor(PropertyId propertyId)
    {
        lock (gate)
        {
            return [.. delivered.Where(message => message.PropertyId == propertyId)];
        }
    }
}
