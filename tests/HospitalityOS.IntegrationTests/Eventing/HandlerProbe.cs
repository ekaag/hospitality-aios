using System.Collections.Concurrent;

namespace HospitalityOS.IntegrationTests.Eventing;

/// <summary>
/// Lets a test count handler runs per organization, inject failures, and hold concurrent runs at a barrier.
/// </summary>
internal sealed class HandlerProbe
{
    private readonly ConcurrentDictionary<Guid, int> runs = new();
    private readonly ConcurrentDictionary<Guid, int> failuresLeft = new();
    private TaskCompletionSource? barrier;
    private int barrierWaiting;
    private int barrierSize;

    public int RunsFor(Guid organizationId) => runs.GetValueOrDefault(organizationId);

    public void FailNext(Guid organizationId, int times) => failuresLeft[organizationId] = times;

    /// <summary>
    /// Holds runs until <paramref name="size"/> of them have arrived, so they all pass the inbox check together.
    /// </summary>
    public void HoldUntil(int size)
    {
        barrierSize = size;
        barrier = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    public async Task BeforeEffectAsync(Guid organizationId, CancellationToken cancellationToken)
    {
        runs.AddOrUpdate(organizationId, 1, (_, count) => count + 1);

        if (failuresLeft.TryGetValue(organizationId, out int left) && left > 0)
        {
            failuresLeft[organizationId] = left - 1;

            throw new InvalidOperationException($"Injected failure for {organizationId}.");
        }

        if (barrier is { } held)
        {
            if (Interlocked.Increment(ref barrierWaiting) == barrierSize)
            {
                held.TrySetResult();
            }

            await held.Task.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken);
        }
    }
}
