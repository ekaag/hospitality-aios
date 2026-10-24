using HospitalityOS.Bindings;
using HospitalityOS.Ports.Bindings;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HospitalityOS.UnitTests.Bindings.TestDoubles;

/// <summary>
/// Builds a service provider with the binding mechanism, the <see cref="IGreeter"/> port, and
/// the given adapter factories, configured from in-memory settings.
/// </summary>
internal static class BindingTestHost
{
    public static ServiceProvider Build(IDictionary<string, string?> settings, params GreeterFactory[] factories)
    {
        IConfiguration configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        ServiceCollection services = new();

        services.AddBindings(configuration);
        services.AddPortBinding<IGreeter>();

        foreach (GreeterFactory factory in factories)
        {
            services.AddSingleton<IAdapterFactory<IGreeter>>(factory);
        }

        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
    }
}
