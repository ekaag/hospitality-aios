using System.Collections.Concurrent;

namespace HospitalityOS.IntegrationTests.Capabilities.TestCapabilities;

/// <summary>
/// Counts how often each test capability actually ran, and can hold runs until several have started, to race them.
/// Singleton.
/// </summary>
internal sealed class CapabilityProbe
{
    private readonly ConcurrentDictionary<string, int> runs = new(StringComparer.Ordinal);
    private int expectedRacers;
    private int arrived;
    private TaskCompletionSource allArrived = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public int Runs(string capability) => runs.GetValueOrDefault(capability);

    public void Ran(string capability) => runs.AddOrUpdate(capability, 1, (_, count) => count + 1);

    public void HoldUntil(int racers)
    {
        expectedRacers = racers;
        arrived = 0;
        allArrived = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    public Task ArriveAsync(CancellationToken cancellationToken)
    {
        if (expectedRacers == 0)
        {
            return Task.CompletedTask;
        }

        if (Interlocked.Increment(ref arrived) >= expectedRacers)
        {
            allArrived.TrySetResult();
        }

        return allArrived.Task.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken);
    }
}
