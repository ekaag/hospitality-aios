using HospitalityOS.Ports.Scheduling;
using HospitalityOS.SharedKernel.Tenancy;
using Xunit;

namespace HospitalityOS.PortContractTests.Scheduling;

/// <summary>
/// What every <see cref="IScheduler"/> adapter must do: deliver a message once it is due and never before, at once when
/// it is already due, and messages that fell due before anyone subscribed once someone does. Times are fractions of
/// <see cref="Lead"/>, so an adapter on a fake clock and one on a broker's real clock run the same checks.
/// </summary>
public abstract class SchedulerContractTests : IAsyncDisposable
{
    private static readonly TimeSpan timeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan quiet = TimeSpan.FromMilliseconds(300);

    private readonly Lazy<(IScheduler Scheduler, IAsyncDisposable Owner)> created;

    protected SchedulerContractTests()
    {
        created = new Lazy<(IScheduler, IAsyncDisposable)>(CreateScheduler);
    }

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    private IScheduler Scheduler => created.Value.Scheduler;

    [Fact]
    public async Task AMessage_IsDeliveredOnceDue_AndNotBefore()
    {
        TaskCompletionSource<ScheduledMessage> delivered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        await using IAsyncDisposable subscription = await Scheduler.SubscribeAsync((message, _) => Deliver(delivered, message), Cancellation);
        ScheduledMessage due = Message(Now + Lead);

        await Scheduler.ScheduleAsync(due, Cancellation);
        await PassAsync(Lead / 2);
        await Task.Delay(quiet, Cancellation);
        bool early = delivered.Task.IsCompleted;
        await PassAsync(Lead / 2);

        Assert.False(early);
        Assert.Equal(due, await delivered.Task.WaitAsync(timeout, Cancellation));
    }

    [Fact]
    public async Task AMessageAlreadyDue_IsDeliveredAtOnce()
    {
        TaskCompletionSource<ScheduledMessage> delivered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        await using IAsyncDisposable subscription = await Scheduler.SubscribeAsync((message, _) => Deliver(delivered, message), Cancellation);
        ScheduledMessage due = Message(Now - (Lead / 10));

        await Scheduler.ScheduleAsync(due, Cancellation);

        Assert.Equal(due, await delivered.Task.WaitAsync(timeout, Cancellation));
    }

    [Fact]
    public async Task AMessageDueBeforeAnyoneSubscribed_IsDeliveredOnSubscribing()
    {
        ScheduledMessage due = Message(Now + (Lead / 10));
        await Scheduler.ScheduleAsync(due, Cancellation);
        await PassAsync(Lead / 5);
        TaskCompletionSource<ScheduledMessage> delivered = new(TaskCreationOptions.RunContinuationsAsynchronously);

        await using IAsyncDisposable subscription = await Scheduler.SubscribeAsync((message, _) => Deliver(delivered, message), Cancellation);

        Assert.Equal(due, await delivered.Task.WaitAsync(timeout, Cancellation));
    }

    public async ValueTask DisposeAsync()
    {
        if (created.IsValueCreated)
        {
            await created.Value.Owner.DisposeAsync();
        }

        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// The adapter under test, on a clock that <see cref="PassAsync"/> moves on, and what disposes it. Called on first use
    /// in a test, so it may skip the test.
    /// </summary>
    protected abstract (IScheduler Scheduler, IAsyncDisposable Owner) CreateScheduler();

    /// <summary>
    /// Where the adapter's clock stands.
    /// </summary>
    protected abstract DateTimeOffset Now { get; }

    /// <summary>
    /// How far ahead the tests schedule: minutes on a fake clock, a few seconds on a real one.
    /// </summary>
    protected virtual TimeSpan Lead => TimeSpan.FromMinutes(10);

    /// <summary>
    /// Lets time pass on the adapter's clock: moves a fake clock on, or waits.
    /// </summary>
    protected abstract Task PassAsync(TimeSpan time);

    private static ScheduledMessage Message(DateTimeOffset dueAt) =>
        new(Guid.CreateVersion7(), "humanaction.reroute_unaccepted", new TenantId(Guid.CreateVersion7()), Guid.CreateVersion7(), null, """{"humanActionId":"x"}""", dueAt);

    private static Task Deliver(TaskCompletionSource<ScheduledMessage> delivered, ScheduledMessage message)
    {
        delivered.TrySetResult(message);

        return Task.CompletedTask;
    }
}
