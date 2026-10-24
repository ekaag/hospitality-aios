using System.Collections.Concurrent;
using HospitalityOS.Bindings.Configuration;
using HospitalityOS.Ports.Bindings;

namespace HospitalityOS.Bindings;

/// <summary>
/// Resolves <typeparamref name="TPort"/> from configuration and caches one adapter instance per binding.
/// Configuration is read once; changes require a restart.
/// </summary>
internal sealed class BindingResolver<TPort> : IBindingResolver<TPort>, IDisposable
    where TPort : class
{
    private readonly IServiceProvider services;
    private readonly PortRegistration<TPort> registration;
    private readonly Dictionary<string, IAdapterFactory<TPort>> factories;
    private readonly Lazy<PortBindingConfiguration> configuration;
    private readonly ConcurrentDictionary<string, Lazy<TPort>> instances = new(StringComparer.Ordinal);

    public BindingResolver(
        IServiceProvider services,
        PortRegistration<TPort> registration,
        IEnumerable<IAdapterFactory<TPort>> factories,
        BindingRoot root)
    {
        this.services = services;
        this.registration = registration;
        this.factories = factories.ToDictionary(factory => factory.AdapterName, StringComparer.OrdinalIgnoreCase);
        configuration = new Lazy<PortBindingConfiguration>(
            () => PortBindingConfiguration.FromSection(root.Section.GetSection(registration.Key)));
    }

    public PortBindingConfiguration Configuration => configuration.Value;

    public IReadOnlyCollection<string> AdapterNames => factories.Keys;

    public TPort Resolve(BindingContext context)
    {
        AdapterBinding binding = Configuration.Select(context)
            ?? throw new BindingException(
                $"No binding configured for port '{registration.Key}' ({typeof(TPort).Name}) and {context}. Add '{Configuration.Path}:{PortBindingConfiguration.DefaultKey}'.");

        return Resolve(binding);
    }

    public TPort Resolve(AdapterBinding binding)
    {
        return instances
            .GetOrAdd(binding.Path, _ => new Lazy<TPort>(() => Create(binding), LazyThreadSafetyMode.ExecutionAndPublication))
            .Value;
    }

    public IAdapterFactory<TPort>? FindFactory(string adapterName)
    {
        return factories.GetValueOrDefault(adapterName);
    }

    public void Dispose()
    {
        foreach (Lazy<TPort> instance in instances.Values.Where(instance => instance.IsValueCreated))
        {
            (instance.Value as IDisposable)?.Dispose();
        }

        instances.Clear();
    }

    private TPort Create(AdapterBinding binding)
    {
        IAdapterFactory<TPort> factory = FindFactory(binding.Adapter)
            ?? throw new BindingException(
                $"'{binding.Path}' selects adapter '{binding.Adapter}', but no such adapter is registered for {typeof(TPort).Name}. Registered: {DescribeAdapters()}.");

        return factory.Create(services, binding.Settings);
    }

    private string DescribeAdapters()
    {
        return factories.Count == 0 ? "(none)" : string.Join(", ", factories.Keys.Order(StringComparer.OrdinalIgnoreCase));
    }
}
