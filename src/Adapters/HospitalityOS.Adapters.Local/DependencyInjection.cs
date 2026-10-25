using HospitalityOS.Adapters.Local.IdGenerator;
using HospitalityOS.Ports.Bindings;
using HospitalityOS.Ports.Ids;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace HospitalityOS.Adapters.Local;

/// <summary>
/// Registers the local/dev adapter factories so configuration can bind ports to them.
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Registers local adapters: <c>GuidV7</c> for <see cref="IIdGenerator"/>.
    /// </summary>
    public static IServiceCollection AddLocalAdapters(this IServiceCollection services)
    {
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IAdapterFactory<IIdGenerator>, GuidV7IdGeneratorFactory>());

        return services;
    }
}
