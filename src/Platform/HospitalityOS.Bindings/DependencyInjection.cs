using HospitalityOS.Ports.Bindings;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace HospitalityOS.Bindings;

/// <summary>
/// Registers the binding mechanism (implementation plan §2.4).
/// </summary>
public static class DependencyInjection
{
    public const string HealthCheckName = "bindings";

    /// <summary>
    /// Registers binding resolution, startup validation, and the <c>Bindings</c> configuration section.
    /// Call once from the host; then declare each port with <see cref="AddPortBinding{TPort}"/>.
    /// </summary>
    public static IServiceCollection AddBindings(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        services.TryAddSingleton(new BindingRoot(configuration.GetSection(BindingRoot.SectionName)));
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<BindingStartupCheck>, BindingStartupValidator>());
        services.AddOptions<BindingStartupCheck>().ValidateOnStart();

        return services;
    }

    /// <summary>
    /// Declares a port that must be bound in configuration under <c>Bindings:{key}</c>, and registers
    /// <see cref="IBindingResolver{TPort}"/> for it. Adapters are registered separately as
    /// <see cref="IAdapterFactory{TPort}"/> singletons by their own projects.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="key">Configuration key; defaults to the port type name without its leading <c>I</c>.</param>
    public static IServiceCollection AddPortBinding<TPort>(this IServiceCollection services, string? key = null)
        where TPort : class
    {
        PortRegistration<TPort> registration = new(key ?? DefaultKeyFor(typeof(TPort)));

        services.AddSingleton(registration);
        services.AddSingleton<PortRegistration>(registration);
        services.AddSingleton<IBindingResolver<TPort>, BindingResolver<TPort>>();

        return services;
    }

    /// <summary>
    /// Adds a health check that resolves and probes every configured binding.
    /// </summary>
    public static IHealthChecksBuilder AddBindingHealthChecks(this IHealthChecksBuilder builder)
    {
        return builder.AddCheck<BindingsHealthCheck>(HealthCheckName);
    }

    private static string DefaultKeyFor(Type portType)
    {
        string name = portType.Name;

        return name.Length > 1 && name[0] == 'I' && char.IsUpper(name[1]) ? name[1..] : name;
    }
}
