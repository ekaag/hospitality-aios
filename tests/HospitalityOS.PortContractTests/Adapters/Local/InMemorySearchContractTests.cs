using HospitalityOS.Adapters.Local;
using HospitalityOS.PortContractTests.Search;
using HospitalityOS.Ports.Bindings;
using HospitalityOS.Ports.Search;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HospitalityOS.PortContractTests.Adapters.Local;

/// <summary>
/// The in-memory keyword index, created the way bindings create it: through its registered adapter factory.
/// </summary>
public sealed class InMemorySearchIndexContractTests : SearchIndexContractTests
{
    protected override ISearchIndex CreateIndex() => InMemorySearch.Create<ISearchIndex>();
}

/// <summary>
/// The in-memory vector store, created through its registered adapter factory.
/// </summary>
public sealed class InMemoryVectorStoreContractTests : VectorStoreContractTests
{
    protected override IVectorStore CreateStore() => InMemorySearch.Create<IVectorStore>();
}

internal static class InMemorySearch
{
    public static TPort Create<TPort>()
        where TPort : class
    {
        using ServiceProvider services = new ServiceCollection().AddLocalAdapters().BuildServiceProvider();

        return services.GetServices<IAdapterFactory<TPort>>().Single(factory => factory.AdapterName == "InMemory")
            .Create(services, new ConfigurationBuilder().Build());
    }
}
