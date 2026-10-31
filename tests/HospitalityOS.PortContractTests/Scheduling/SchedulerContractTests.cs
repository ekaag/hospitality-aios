using HospitalityOS.Ports.Scheduling;
using HospitalityOS.SharedKernel.Tenancy;
using Xunit;

namespace HospitalityOS.PortContractTests.Scheduling;

/// <summary>
/// What every <see cref="IScheduler"/> adapter must do: deliver a message once it is due and never before, at once when
/// it is already due, and messages that fell due before anyone subscribed once someone does.
/// </summary>
public abstract class SchedulerContractTests : IDisposable
{
    private static readonly TimeSpan timeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan quiet = TimeSpan.FromMilliseconds(300);

    private readonly Lazy<(IScheduler Scheduler, IDisposable Owner)> created;

    protected SchedulerContractTests()
    {
        created = new Lazy<(IScheduler, IDisposable)>(CreateScheduler);
    }

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    private IScheduler Scheduler => created.Value.Scheduler;

    [Fact]
    public async Task AMessage_IsDeliveredOnceDue_AndNotBefore()
    {
        TaskCompletionSource<ScheduledMessage> delivered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        await using IAsyncDisposable subscription = await Scheduler.SubscribeAsync((message, _) => Deliver(delivered, message), Cancellation);
        ScheduledMessage due = Message(Now + TimeSpan.FromMinutes(10));

        await Scheduler.ScheduleAsync(due, Cancellation);
        Advance(TimeSpan.FromMinutes(9));
        await Task.Delay(quiet, Cancellation);
        bool early = delivered.Task.IsCompleted;
        Advance(TimeSpan.FromMinutes(1));

        Assert.False(early);
        Assert.Equal(due, await delivered.Task.WaitAsync(timeout, Cancellation));
    }

    [Fact]
    public async Task AMessageAlreadyDue_IsDeliveredAtOnce()
    {
        TaskCompletionSource<ScheduledMessage> delivered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        await using IAsyncDisposable subscription = await Scheduler.SubscribeAsync((message, _) => Deliver(delivered, message), Cancellation);
        ScheduledMessage due = Message(Now - TimeSpan.FromMinutes(1));

        await Scheduler.ScheduleAsync(due, Cancellation);

        Assert.Equal(due, await delivered.Task.WaitAsync(timeout, Cancellation));
    }

    [Fact]
    public async Task AMessageDueBeforeAnyoneSubscribed_IsDeliveredOnSubscribing()
    {
        ScheduledMessage due = Message(Now + TimeSpan.FromMinutes(1));
        await Scheduler.ScheduleAsync(due, Cancellation);
        Advance(TimeSpan.FromMinutes(2));
        TaskCompletionSource<ScheduledMessage> delivered = new(TaskCreationOptions.RunContinuationsAsynchronously);

        await using IAsyncDisposable subscription = await Scheduler.SubscribeAsync((message, _) => Deliver(delivered, message), Cancellation);

        Assert.Equal(due, await delivered.Task.WaitAsync(timeout, Cancellation));
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
    /// The adapter under test, on a clock the test moves with <see cref="Advance"/>, and what disposes it.
    /// </summary>
    protected abstract (IScheduler Scheduler, IDisposable Owner) CreateScheduler();

    /// <summary>
    /// Where the adapter's clock stands.
    /// </summary>
    protected abstract DateTimeOffset Now { get; }

    /// <summary>
    /// Moves the adapter's clock on.
    /// </summary>
    protected abstract void Advance(TimeSpan by);

    private static ScheduledMessage Message(DateTimeOffset dueAt) =>
        new(Guid.CreateVersion7(), "humanaction.reroute_unaccepted", new TenantId(Guid.CreateVersion7()), Guid.CreateVersion7(), null, """{"humanActionId":"x"}""", dueAt);

    private static Task Deliver(TaskCompletionSource<ScheduledMessage> delivered, ScheduledMessage message)
    {
        delivered.TrySetResult(message);

        return Task.CompletedTask;
    }
}
