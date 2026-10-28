using System.Collections.Concurrent;

namespace HospitalityOS.Bindings.Inference;

/// <summary>
/// Everything the router needs, read once: the configuration, the declared tasks, the adapters per inference port, and
/// one cached adapter instance per provider and port (instances are shared, so adapters must be thread-safe).
/// Singleton.
/// </summary>
internal sealed class InferenceCatalog : IDisposable
{
    private readonly IServiceProvider services;
    private readonly Lazy<InferenceConfiguration> configuration;
    private readonly Dictionary<Type, InferencePortAdapters> adapters;
    private readonly ConcurrentDictionary<(string Provider, Type Port), Lazy<object>> instances = new();

    public InferenceCatalog(
        IServiceProvider services,
        InferenceRoot root,
        IEnumerable<InferenceTaskRegistration> tasks,
        IEnumerable<InferencePortAdapters> adapters)
    {
        ArgumentNullException.ThrowIfNull(root);

        this.services = services;
        configuration = new Lazy<InferenceConfiguration>(() => InferenceConfiguration.FromSection(root.Section));
        Tasks = [.. tasks];
        this.adapters = adapters.ToDictionary(port => port.Port);
    }

    public InferenceConfiguration Configuration => configuration.Value;

    /// <summary>
    /// Declared tasks, possibly with duplicates (startup validation reports conflicting ones).
    /// </summary>
    public IReadOnlyList<InferenceTaskRegistration> Tasks { get; }

    public IReadOnlyCollection<InferencePortAdapters> Ports => adapters.Values;

    public InferenceTaskRegistration? FindTask(string task) =>
        Tasks.FirstOrDefault(registration => string.Equals(registration.Name, task, StringComparison.OrdinalIgnoreCase));

    public InferencePortAdapters? AdaptersFor(Type port) => adapters.GetValueOrDefault(port);

    /// <summary>
    /// The provider's adapter instance for <paramref name="port"/>, created on first use.
    /// </summary>
    /// <exception cref="BindingException">The provider is unknown or its adapter does not serve the port (startup
    /// validation reports both first).</exception>
    public object Instance(string provider, Type port) =>
        instances.GetOrAdd((provider.ToUpperInvariant(), port), _ => new Lazy<object>(() => Create(provider, port), LazyThreadSafetyMode.ExecutionAndPublication)).Value;

    public void Dispose()
    {
        foreach (Lazy<object> instance in instances.Values.Where(instance => instance.IsValueCreated))
        {
            (instance.Value as IDisposable)?.Dispose();
        }

        instances.Clear();
    }

    private object Create(string providerName, Type port)
    {
        InferenceProvider provider = Configuration.Providers.GetValueOrDefault(providerName)
            ?? throw new BindingException($"No inference provider '{providerName}' is configured under '{Configuration.Path}:{InferenceConfiguration.ProvidersKey}'.");
        InferencePortAdapters? portAdapters = AdaptersFor(port);

        if (portAdapters is null || !portAdapters.Has(provider.Adapter))
        {
            throw new BindingException($"Inference provider '{provider.Name}' uses adapter '{provider.Adapter}', which does not serve {port.Name}.");
        }

        return portAdapters.Create(services, provider.Adapter, provider.Settings);
    }
}
