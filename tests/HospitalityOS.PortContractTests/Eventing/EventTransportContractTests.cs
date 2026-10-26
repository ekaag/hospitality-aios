using System.Collections.Concurrent;
using HospitalityOS.Ports.Eventing;
using Xunit;

namespace HospitalityOS.PortContractTests.Eventing;

/// <summary>
/// What every transport guarantees (P1, spec §37): each subscription gets each event, in order; a failing handler
/// gets redeliveries and then a dead letter, without blocking later events. Waits are bounded and signal-driven,
/// never sleeps.
/// </summary>
public abstract class EventTransportContractTests : IDisposable
{
    /// <summary>
    /// Deliveries per event before it is dead-lettered, as configured by <see cref="CreateTransport"/>.
    /// </summary>
    protected const int MaxDeliveryCount = 3;

    private static readonly TimeSpan timeout = TimeSpan.FromSeconds(10);

    private readonly Lazy<(IEventTransport Transport, IDisposable Owner)> created;

    protected EventTransportContractTests()
    {
        created = new Lazy<(IEventTransport, IDisposable)>(CreateTransport);
    }

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    private IEventTransport Transport => created.Value.Transport;

    [Fact]
    public async Task Publish_DeliversToEverySubscription()
    {
        TaskCompletionSource<EventMessage> housekeeping = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource<EventMessage> messaging = new(TaskCreationOptions.RunContinuationsAsynchronously);
        await using IAsyncDisposable first = await SubscribeAsync("housekeeping", message => housekeeping.TrySetResult(message));
        await using IAsyncDisposable second = await SubscribeAsync("messaging", message => messaging.TrySetResult(message));

        await Transport.PublishAsync(TestEvents.Message(1), Cancellation);

        Assert.Equal(TestEvents.Message(1), await housekeeping.Task.WaitAsync(timeout, Cancellation));
        Assert.Equal(TestEvents.Message(1), await messaging.Task.WaitAsync(timeout, Cancellation));
    }

    [Fact]
    public async Task Publish_SubscriptionReceivesEventsInPublishOrder()
    {
        ConcurrentQueue<EventMessage> received = [];
        TaskCompletionSource allReceived = new(TaskCreationOptions.RunContinuationsAsynchronously);
        await using IAsyncDisposable subscription = await SubscribeAsync("ordered", message =>
        {
            received.Enqueue(message);

            if (received.Count == 5)
            {
                allReceived.TrySetResult();
            }
        });

        foreach (int seed in Enumerable.Range(1, 5))
        {
            await Transport.PublishAsync(TestEvents.Message(seed), Cancellation);
        }

        await allReceived.Task.WaitAsync(timeout, Cancellation);
        Assert.Equal(Enumerable.Range(1, 5).Select(seed => TestEvents.Message(seed)), received);
    }

    [Fact]
    public async Task Publish_AfterSubscriptionDisposed_IsNotDeliveredToIt()
    {
        ConcurrentQueue<EventMessage> stopped = [];
        TaskCompletionSource<EventMessage> probe = new(TaskCreationOptions.RunContinuationsAsynchronously);
        IAsyncDisposable subscription = await SubscribeAsync("stopped", stopped.Enqueue);
        await subscription.DisposeAsync();
        await using IAsyncDisposable probing = await SubscribeAsync("probe", message => probe.TrySetResult(message));

        await Transport.PublishAsync(TestEvents.Message(1), Cancellation);

        await probe.Task.WaitAsync(timeout, Cancellation);
        Assert.Empty(stopped);
    }

    [Fact]
    public async Task Deliver_HandlerFailsThenSucceeds_IsRedeliveredWithoutDeadLetter()
    {
        int deliveries = 0;
        TaskCompletionSource succeeded = new(TaskCreationOptions.RunContinuationsAsynchronously);
        await using IAsyncDisposable subscription = await Transport.SubscribeAsync("flaky", (_, _) =>
        {
            if (Interlocked.Increment(ref deliveries) < MaxDeliveryCount)
            {
                throw new InvalidOperationException("transient");
            }

            succeeded.TrySetResult();

            return Task.CompletedTask;
        }, Cancellation);

        await Transport.PublishAsync(TestEvents.Message(1), Cancellation);

        await succeeded.Task.WaitAsync(timeout, Cancellation);
        Assert.Equal(MaxDeliveryCount, deliveries);
        Assert.Empty(await Transport.ReadDeadLettersAsync("flaky", Cancellation));
    }

    [Fact]
    public async Task Deliver_HandlerAlwaysFails_DeadLettersEventAndContinuesWithNext()
    {
        TaskCompletionSource<EventMessage> next = new(TaskCreationOptions.RunContinuationsAsynchronously);
        await using IAsyncDisposable subscription = await Transport.SubscribeAsync("poisoned", (message, _) =>
        {
            if (message == TestEvents.Message(1))
            {
                throw new InvalidOperationException("room not found");
            }

            next.TrySetResult(message);

            return Task.CompletedTask;
        }, Cancellation);

        await Transport.PublishAsync(TestEvents.Message(1), Cancellation);
        await Transport.PublishAsync(TestEvents.Message(2), Cancellation);

        Assert.Equal(TestEvents.Message(2), await next.Task.WaitAsync(timeout, Cancellation));
        DeadLetteredEvent deadLettered = Assert.Single(await Transport.ReadDeadLettersAsync("poisoned", Cancellation));
        Assert.Equal(TestEvents.Message(1), deadLettered.Message);
        Assert.Equal(MaxDeliveryCount, deadLettered.DeliveryCount);
        Assert.Contains("room not found", deadLettered.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Subscribe_NameAlreadyActive_Throws()
    {
        await using IAsyncDisposable subscription = await SubscribeAsync("single", _ => { });

        await Assert.ThrowsAsync<InvalidOperationException>(() => SubscribeAsync("single", _ => { }));
    }

    public void Dispose()
    {
        if (created.IsValueCreated)
        {
            created.Value.Owner.Dispose();
        }

        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Creates the transport under test with <see cref="MaxDeliveryCount"/> deliveries and no redelivery delay, and
    /// whatever owns it (disposed after the test).
    /// </summary>
    protected abstract (IEventTransport Transport, IDisposable Owner) CreateTransport();

    private Task<IAsyncDisposable> SubscribeAsync(string name, Action<EventMessage> onMessage) =>
        Transport.SubscribeAsync(name, (message, _) =>
        {
            onMessage(message);

            return Task.CompletedTask;
        }, Cancellation);
}
