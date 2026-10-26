using HospitalityOS.Capabilities.Definitions;
using HospitalityOS.Capabilities.Registry;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace HospitalityOS.Capabilities;

/// <summary>
/// Registers the capability registry and the capabilities in it.
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Registers <see cref="CapabilityRegistry"/> over every capability added with <see cref="AddCapability{TCapability}"/>.
    /// </summary>
    public static IServiceCollection AddCapabilities(this IServiceCollection services)
    {
        services.TryAddSingleton<CapabilityRegistry>();

        return services;
    }

    /// <summary>
    /// Registers <typeparamref name="TCapability"/>. Its definition is built and checked here, so an invalid
    /// declaration, or a second registration of the same name and version, fails at startup.
    /// </summary>
    /// <exception cref="InvalidOperationException">The declaration is invalid or already registered.</exception>
    public static IServiceCollection AddCapability<TCapability>(this IServiceCollection services)
        where TCapability : class, ICapability
    {
        CapabilityDefinition definition = CapabilityDefinition.For<TCapability>();

        if (services.Any(descriptor => descriptor.ImplementationInstance is CapabilityDefinition existing
            && existing.Name == definition.Name
            && existing.Version == definition.Version))
        {
            throw new InvalidOperationException($"Capability {definition} is already registered; a name and version can be registered once.");
        }

        services.AddSingleton(definition);

        return services;
    }
}
