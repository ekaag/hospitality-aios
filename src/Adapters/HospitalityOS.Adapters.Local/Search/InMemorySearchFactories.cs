using HospitalityOS.Ports.Bindings;
using HospitalityOS.Ports.Search;
using Microsoft.Extensions.Configuration;

namespace HospitalityOS.Adapters.Local.Search;

/// <summary>
/// Binds <see cref="ISearchIndex"/> to <see cref="InMemorySearchIndex"/> under the adapter name <c>InMemory</c>. No settings.
/// </summary>
internal sealed class InMemorySearchIndexFactory : IAdapterFactory<ISearchIndex>
{
    public string AdapterName => "InMemory";

    public ISearchIndex Create(IServiceProvider services, IConfiguration settings) => new InMemorySearchIndex();
}

/// <summary>
/// Binds <see cref="IVectorStore"/> to <see cref="InMemoryVectorStore"/> under the adapter name <c>InMemory</c>. No settings.
/// </summary>
internal sealed class InMemoryVectorStoreFactory : IAdapterFactory<IVectorStore>
{
    public string AdapterName => "InMemory";

    public IVectorStore Create(IServiceProvider services, IConfiguration settings) => new InMemoryVectorStore();
}
