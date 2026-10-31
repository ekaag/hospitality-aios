using HospitalityOS.Ports.Bindings;
using HospitalityOS.Ports.Scheduling;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HospitalityOS.Adapters.Local.Scheduling;

/// <summary>
/// Creates <see cref="InProcessScheduler"/>s (<c>InProcess</c>). No settings.
/// </summary>
internal sealed class InProcessSchedulerFactory : IAdapterFactory<IScheduler>
{
    public const string Name = "InProcess";

    public string AdapterName => Name;

    public IScheduler Create(IServiceProvider services, IConfiguration settings) =>
        new InProcessScheduler(services.GetService<TimeProvider>() ?? TimeProvider.System);

    public IEnumerable<string> ValidateSettings(IConfiguration settings) => [];
}
