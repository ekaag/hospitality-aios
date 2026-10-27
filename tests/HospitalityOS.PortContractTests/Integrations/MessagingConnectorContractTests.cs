using HospitalityOS.IntegrationContracts;
using HospitalityOS.IntegrationContracts.Messaging;
using HospitalityOS.SharedKernel.Ids;
using HospitalityOS.SharedKernel.Results;
using Xunit;
using static HospitalityOS.PortContractTests.Integrations.ConnectorTestData;

namespace HospitalityOS.PortContractTests.Integrations;

/// <summary>
/// What every <see cref="IMessagingConnector"/> provider must do: above all, deliver each logical message once (P8,
/// Journey 19), even when a connector is recreated after a configuration change.
/// </summary>
public abstract class MessagingConnectorContractTests
{
    private static readonly MessageRecipient alex = new(MessageChannel.Web, "conversation-0412");

    protected static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    /// <summary>
    /// A recipient the provider refuses as unreachable.
    /// </summary>
    protected abstract MessageRecipient UnreachableRecipient { get; }

    /// <summary>
    /// A new connector for <paramref name="propertyId"/> over the provider's one messaging system.
    /// </summary>
    protected abstract IMessagingConnector CreateConnector(PropertyId propertyId);

    /// <summary>
    /// What the provider delivered to the property's guests, oldest first.
    /// </summary>
    protected abstract IReadOnlyList<OutboundMessage> Delivered(PropertyId propertyId);

    [Fact]
    public async Task Send_DeliversTheMessageAndReturnsAReceipt()
    {
        OutboundMessage message = Message("towels-done");

        Result<MessageReceipt> receipt = await CreateConnector(AuroraGrand).SendAsync(message, Cancellation);

        Assert.True(receipt.IsSuccess);
        Assert.Equal([message], Delivered(AuroraGrand));
    }

    [Fact]
    public async Task Send_TheSameKeyAgain_ReturnsTheOriginalReceiptAndDeliversNothing()
    {
        Result<MessageReceipt> first = await CreateConnector(AuroraGrand).SendAsync(Message("towels-done"), Cancellation);

        Result<MessageReceipt> again = await CreateConnector(AuroraGrand).SendAsync(Message("towels-done", "Your towels are here!"), Cancellation);

        Assert.Equal(first.Value, again.Value);
        Assert.Single(Delivered(AuroraGrand));
    }

    [Fact]
    public async Task Send_TheSameKeyConcurrently_DeliversOnce()
    {
        IMessagingConnector connector = CreateConnector(AuroraGrand);

        Result<MessageReceipt>[] receipts = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Task.Run(() => connector.SendAsync(Message("towels-done"), Cancellation))));

        Assert.Single(receipts.Select(receipt => receipt.Value).Distinct());
        Assert.Single(Delivered(AuroraGrand));
    }

    [Fact]
    public async Task Send_DifferentKeysOrProperties_AreSeparateMessages()
    {
        await CreateConnector(AuroraGrand).SendAsync(Message("towels-done"), Cancellation);
        await CreateConnector(AuroraGrand).SendAsync(Message("pillow-done"), Cancellation);
        await CreateConnector(Harbourside).SendAsync(Message("towels-done"), Cancellation);

        Assert.Equal(["towels-done", "pillow-done"], Delivered(AuroraGrand).Select(message => message.IdempotencyKey));
        Assert.Single(Delivered(Harbourside));
    }

    [Fact]
    public async Task Send_ToAnUnreachableRecipient_IsRejected_AndARetryOfTheKeyCanStillSucceed()
    {
        IMessagingConnector connector = CreateConnector(AuroraGrand);

        Result<MessageReceipt> rejected = await connector.SendAsync(Message("towels-done") with { Recipient = UnreachableRecipient }, Cancellation);
        Result<MessageReceipt> retried = await connector.SendAsync(Message("towels-done"), Cancellation);

        Assert.Equal((IntegrationErrors.RejectedCode, ErrorKind.InvalidState), (rejected.Error.Code, rejected.Error.Kind));
        Assert.True(retried.IsSuccess);
        Assert.Single(Delivered(AuroraGrand));
    }

    private static OutboundMessage Message(string key, string body = "Your towels have been delivered.") => new(key, alex, body);
}
