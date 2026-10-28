using HospitalityOS.Ports.Bindings;
using Microsoft.Extensions.Configuration;

namespace HospitalityOS.Bindings.Inference;

/// <summary>
/// The adapters registered for one inference port (<see cref="IAdapterFactory{TPort}"/> singletons from adapter
/// projects), so providers can be validated and created without knowing the port type.
/// </summary>
internal abstract class InferencePortAdapters
{
    public abstract Type Port { get; }

    public abstract IReadOnlyCollection<string> AdapterNames { get; }

    public abstract bool Has(string adapter);

    public abstract IEnumerable<string> ValidateSettings(string adapter, IConfiguration settings);

    public abstract object Create(IServiceProvider services, string adapter, IConfiguration settings);
}

/// <summary>
/// The adapters registered for <typeparamref name="TPort"/>.
/// </summary>
internal sealed class InferencePortAdapters<TPort>(IEnumerable<IAdapterFactory<TPort>> factories) : InferencePortAdapters
    where TPort : class
{
    private readonly Dictionary<string, IAdapterFactory<TPort>> byName = factories.ToDictionary(factory => factory.AdapterName, StringComparer.OrdinalIgnoreCase);

    public override Type Port => typeof(TPort);

    public override IReadOnlyCollection<string> AdapterNames => byName.Keys;

    public override bool Has(string adapter) => byName.ContainsKey(adapter);

    public override IEnumerable<string> ValidateSettings(string adapter, IConfiguration settings) => byName[adapter].ValidateSettings(settings);

    public override object Create(IServiceProvider services, string adapter, IConfiguration settings) => byName[adapter].Create(services, settings);
}
