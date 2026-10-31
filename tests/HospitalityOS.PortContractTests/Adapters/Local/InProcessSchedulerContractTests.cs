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

    protected override (IScheduler Scheduler, IDisposable Owner) CreateScheduler()
    {
        ServiceProvider services = new ServiceCollection().AddSingleton<TimeProvider>(clock).AddLocalAdapters().BuildServiceProvider();
        IAdapterFactory<IScheduler> factory = services.GetServices<IAdapterFactory<IScheduler>>().Single(candidate => candidate.AdapterName == "InProcess");
        IScheduler scheduler = factory.Create(services, new ConfigurationBuilder().Build());

        return (scheduler, new Owner(scheduler, services));
    }

    protected override void Advance(TimeSpan by) => clock.Advance(by);

    private sealed class Owner(IScheduler scheduler, ServiceProvider services) : IDisposable
    {
        public void Dispose()
        {
            (scheduler as IDisposable)?.Dispose();
            services.Dispose();
        }
    }
}
