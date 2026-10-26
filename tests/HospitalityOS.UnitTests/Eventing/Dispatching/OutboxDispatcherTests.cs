using HospitalityOS.Eventing.Dispatching;
using HospitalityOS.Ports.Eventing;
using HospitalityOS.SharedKernel.Tenancy;
using HospitalityOS.UnitTests.Eventing.TestDoubles;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace HospitalityOS.UnitTests.Eventing.Dispatching;

public sealed class OutboxDispatcherTests
{
    private static readonly DateTimeOffset now = new(2026, 10, 24, 19, 30, 0, TimeSpan.Zero);

    private static readonly EventDispatcherOptions options = new()
    {
        BatchSize = 25,
        Lease = TimeSpan.FromSeconds(45),
        MaxAttempts = 4,
        InitialRetryDelay = TimeSpan.FromSeconds(1),
        MaxRetryDelay = TimeSpan.FromSeconds(10),
    };

    private readonly RecordingTransport transport = new();

    [Fact]
    public async Task DispatchOnce_ClaimsOneBatchAtTheCurrentTimeWithTheConfiguredLease()
    {
        RecordingOutboxStore outbox = new();

        await Dispatcher(outbox).DispatchOnceAsync(TestContext.Current.CancellationToken);

        Assert.Equal([(now, 25, TimeSpan.FromSeconds(45))], outbox.Claims);
    }

    [Fact]
    public async Task DispatchOnce_PublishesAndRemovesEachClaimedEventInOrder()
    {
        RecordingOutboxStore outbox = new(Entry(1), Entry(2));

        int claimed = await Dispatcher(outbox).DispatchOnceAsync(TestContext.Current.CancellationToken);

        Assert.Equal(2, claimed);
        Assert.Equal([Entry(1).Message, Entry(2).Message], transport.Published);
        Assert.Equal([Id(1), Id(2)], outbox.Dispatched);
    }

    [Fact]
    public async Task DispatchOnce_PublishFails_SchedulesRetryAndContinuesWithTheNextEvent()
    {
        RecordingOutboxStore outbox = new(Entry(1, attempts: 1), Entry(2));
        transport.Failing.Add(Id(1));

        await Dispatcher(outbox).DispatchOnceAsync(TestContext.Current.CancellationToken);

        (Guid eventId, DateTimeOffset retryAt, string error) = Assert.Single(outbox.Retries);
        Assert.Equal((Id(1), now.AddSeconds(2)), (eventId, retryAt));
        Assert.Contains("broker unavailable", error, StringComparison.Ordinal);
        Assert.Equal([Id(2)], outbox.Dispatched);
        Assert.Empty(outbox.DeadLetters);
    }

    [Fact]
    public async Task DispatchOnce_LastAllowedAttemptFails_DeadLettersTheEvent()
    {
        RecordingOutboxStore outbox = new(Entry(1, attempts: options.MaxAttempts - 1));
        transport.Failing.Add(Id(1));

        await Dispatcher(outbox).DispatchOnceAsync(TestContext.Current.CancellationToken);

        (Guid eventId, DateTimeOffset at, string error) = Assert.Single(outbox.DeadLetters);
        Assert.Equal((Id(1), now), (eventId, at));
        Assert.Contains("broker unavailable", error, StringComparison.Ordinal);
        Assert.Empty(outbox.Retries);
        Assert.Empty(outbox.Dispatched);
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 2)]
    [InlineData(3, 4)]
    [InlineData(4, 8)]
    [InlineData(5, 10)]
    [InlineData(1000, 10)]
    public void RetryDelay_DoublesPerFailureUpToTheMaximum(int failedAttempts, int expectedSeconds)
    {
        TimeSpan delay = Dispatcher(new RecordingOutboxStore()).RetryDelay(failedAttempts);

        Assert.Equal(TimeSpan.FromSeconds(expectedSeconds), delay);
    }

    private static Guid Id(int seed) => Guid.Parse($"01890a5d-ac96-774b-bcce-{seed:D12}");

    private static OutboxEntry Entry(int seed, int attempts = 0) => new(
        new EventMessage(Id(seed), "HumanActionCompleted", 1, new TenantId(Id(100)), "HumanAction", Id(200 + seed), Id(300), now, "{}"),
        attempts,
        null);

    private OutboxDispatcher Dispatcher(RecordingOutboxStore outbox) => new(
        () => new DispatcherScope(outbox, NoOwner.Instance),
        transport,
        Options.Create(options),
        new FakeTimeProvider(now),
        NullLogger<OutboxDispatcher>.Instance);

    private sealed class NoOwner : IAsyncDisposable
    {
        public static NoOwner Instance { get; } = new();

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
