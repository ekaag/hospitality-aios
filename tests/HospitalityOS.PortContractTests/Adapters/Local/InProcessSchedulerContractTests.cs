using HospitalityOS.Adapters.Local;
using HospitalityOS.PortContractTests.Scheduling;
using HospitalityOS.Ports.Bindings;
using HospitalityOS.Ports.Scheduling;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace HospitalityOS.PortContractTests.Adapters.Local;

/// <summary>
/// The local <c>InProcess</c> scheduler passes the scheduler contract, on a fake clock.
/// </summary>
public sealed class InProcessSchedulerContractTests : SchedulerContractTests
{
    private readonly FakeTimeProvider clock = new(new DateTimeOffset(2026, 10, 30, 9, 0, 0, TimeSpan.Zero));

    protected override DateTimeOffset Now => clock.GetUtcNow();

    protected override (IScheduler Scheduler, IAsyncDisposable Owner) CreateScheduler()
    {
        ServiceProvider services = new ServiceCollection().AddSingleton<TimeProvider>(clock).AddLocalAdapters().BuildServiceProvider();
        IAdapterFactory<IScheduler> factory = services.GetServices<IAdapterFactory<IScheduler>>().Single(candidate => candidate.AdapterName == "InProcess");
        IScheduler scheduler = factory.Create(services, new ConfigurationBuilder().Build());

        return (scheduler, new Owner(scheduler, services));
    }

    protected override Task PassAsync(TimeSpan time)
    {
        clock.Advance(time);

        return Task.CompletedTask;
    }

    private sealed class Owner(IScheduler scheduler, ServiceProvider services) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            (scheduler as IDisposable)?.Dispose();
            await services.DisposeAsync();
        }
    }
}
