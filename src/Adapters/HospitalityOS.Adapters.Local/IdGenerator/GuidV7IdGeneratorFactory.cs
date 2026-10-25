using HospitalityOS.Ports.Bindings;
using HospitalityOS.Ports.Ids;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HospitalityOS.Adapters.Local.IdGenerator;

/// <summary>
/// Binds <see cref="IIdGenerator"/> to <see cref="GuidV7IdGenerator"/> under the adapter name <c>GuidV7</c>.
/// Uses the registered <see cref="TimeProvider"/>, or the system clock when none is registered.
/// </summary>
internal sealed class GuidV7IdGeneratorFactory : IAdapterFactory<IIdGenerator>
{
    public const string Name = "GuidV7";

    public string AdapterName => Name;

    public IIdGenerator Create(IServiceProvider services, IConfiguration settings)
    {
        return new GuidV7IdGenerator(services.GetService<TimeProvider>() ?? TimeProvider.System);
    }
}
