using HospitalityOS.Adapters.Local.Eventing;
using HospitalityOS.Adapters.Local.IdGenerator;
using HospitalityOS.Adapters.Local.Inference;
using HospitalityOS.Adapters.Local.Search;
using HospitalityOS.Ports.Bindings;
using HospitalityOS.Ports.Eventing;
using HospitalityOS.Ports.Ids;
using HospitalityOS.Ports.Inference;
using HospitalityOS.Ports.Search;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace HospitalityOS.Adapters.Local;

/// <summary>
/// Registers the local/dev adapter factories so configuration can bind ports to them.
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Registers local adapters: <c>GuidV7</c> for <see cref="IIdGenerator"/>, <c>InMemory</c> for <see cref="IEventTransport"/>,
    /// <see cref="ISearchIndex"/>, and <see cref="IVectorStore"/>, and <c>Fake</c> for every inference port (deterministic, optionally scripted through <see cref="FakeInferenceOptions"/>).
    /// </summary>
    public static IServiceCollection AddLocalAdapters(this IServiceCollection services)
    {
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IAdapterFactory<IIdGenerator>, GuidV7IdGeneratorFactory>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IAdapterFactory<IEventTransport>, InMemoryEventTransportFactory>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IAdapterFactory<ISearchIndex>, InMemorySearchIndexFactory>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IAdapterFactory<IVectorStore>, InMemoryVectorStoreFactory>());
        services.AddOptions<FakeInferenceOptions>();
        AddFake<IChatModel>(services);
        AddFake<IStructuredExtractor>(services);
        AddFake<IClassifier>(services);
        AddFake<IEmbeddingModel>(services);
        AddFake<IReranker>(services);
        AddFake<IEvaluationJudge>(services);

        return services;
    }

    private static void AddFake<TPort>(IServiceCollection services)
        where TPort : class
    {
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IAdapterFactory<TPort>, FakeModelFactory<TPort>>());
    }
}
