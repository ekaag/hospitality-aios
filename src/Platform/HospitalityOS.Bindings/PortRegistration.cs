using HospitalityOS.Bindings.Configuration;
using HospitalityOS.Ports.Bindings;
using Microsoft.Extensions.DependencyInjection;

namespace HospitalityOS.Bindings;

/// <summary>
/// A port that must be bound in configuration. Registered by <c>AddPortBinding</c>; used by
/// startup validation and health checks without knowing the port type.
/// </summary>
internal abstract class PortRegistration
{
    protected PortRegistration(string key)
    {
        Key = key;
    }

    /// <summary>
    /// Configuration key under <c>Bindings</c>, e.g. <c>EventTransport</c>.
    /// </summary>
    public string Key { get; }

    public abstract Type PortType { get; }

    public abstract IEnumerable<string> Validate(IServiceProvider services);

    public abstract Task<IReadOnlyList<BindingHealth>> CheckHealthAsync(IServiceProvider services, CancellationToken cancellationToken);
}

/// <summary>
/// Typed registration for <typeparamref name="TPort"/>.
/// </summary>
internal sealed class PortRegistration<TPort> : PortRegistration
    where TPort : class
{
    public PortRegistration(string key)
        : base(key)
    {
    }

    public override Type PortType => typeof(TPort);

    public override IEnumerable<string> Validate(IServiceProvider services)
    {
        BindingResolver<TPort> resolver = GetResolver(services);
        PortBindingConfiguration configuration = resolver.Configuration;

        if (configuration.DefaultBinding is null)
        {
            yield return $"Port '{Key}' ({typeof(TPort).Name}) has no default binding. Add '{configuration.Path}:{PortBindingConfiguration.DefaultKey}'.";
        }

        foreach (AdapterBinding binding in configuration.All())
        {
            if (string.IsNullOrWhiteSpace(binding.Adapter))
            {
                yield return $"'{binding.Path}' must set '{AdapterBinding.AdapterKey}'.";
                continue;
            }

            IAdapterFactory<TPort>? factory = resolver.FindFactory(binding.Adapter);

            if (factory is null)
            {
                string registered = resolver.AdapterNames.Count == 0 ? "(none)" : string.Join(", ", resolver.AdapterNames);
                yield return $"'{binding.Path}' selects unknown adapter '{binding.Adapter}' for {typeof(TPort).Name}. Registered: {registered}.";
                continue;
            }

            foreach (string error in factory.ValidateSettings(binding.Settings))
            {
                yield return $"'{binding.Path}:{AdapterBinding.SettingsKey}': {error}";
            }
        }
    }

    public override async Task<IReadOnlyList<BindingHealth>> CheckHealthAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        BindingResolver<TPort> resolver = GetResolver(services);
        List<BindingHealth> results = [];

        foreach (AdapterBinding binding in resolver.Configuration.All())
        {
            results.Add(await CheckBindingAsync(resolver, binding, cancellationToken).ConfigureAwait(false));
        }

        return results;
    }

    private static async Task<BindingHealth> CheckBindingAsync(
        BindingResolver<TPort> resolver,
        AdapterBinding binding,
        CancellationToken cancellationToken)
    {
        try
        {
            TPort adapter = resolver.Resolve(binding);

            AdapterHealth health = adapter is IAdapterHealthProbe probe
                ? await probe.CheckHealthAsync(cancellationToken).ConfigureAwait(false)
                : AdapterHealth.Healthy;

            return new BindingHealth(binding.Path, binding.Adapter, health);
        }
#pragma warning disable CA1031 // A health check reports any adapter failure instead of throwing.
        catch (Exception exception)
#pragma warning restore CA1031
        {
            return new BindingHealth(binding.Path, binding.Adapter, AdapterHealth.Unhealthy(exception.Message));
        }
    }

    private static BindingResolver<TPort> GetResolver(IServiceProvider services)
    {
        return (BindingResolver<TPort>)services.GetRequiredService<IBindingResolver<TPort>>();
    }
}
