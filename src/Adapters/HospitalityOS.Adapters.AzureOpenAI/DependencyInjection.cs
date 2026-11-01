using HospitalityOS.Ports.Bindings;
using HospitalityOS.Ports.Inference;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace HospitalityOS.Adapters.AzureOpenAI;

/// <summary>
/// Registers the Azure OpenAI (Foundry) inference adapter so configuration can bind providers to it.
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Registers <c>AzureOpenAI</c> for <see cref="IChatModel"/>, <see cref="IStructuredExtractor"/>, <see cref="IClassifier"/>,
    /// <see cref="IEvaluationJudge"/>, and <see cref="IEmbeddingModel"/>; it serves no reranking models. Provider settings:
    /// <c>Endpoint</c>, <c>MaxRetries</c>, <c>TimeoutSeconds</c>.
    /// </summary>
    public static IServiceCollection AddAzureOpenAIAdapters(this IServiceCollection services)
    {
        Add<IChatModel>(services);
        Add<IStructuredExtractor>(services);
        Add<IClassifier>(services);
        Add<IEvaluationJudge>(services);
        Add<IEmbeddingModel>(services);

        return services;
    }

    private static void Add<TPort>(IServiceCollection services)
        where TPort : class
    {
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IAdapterFactory<TPort>, AzureOpenAIModelFactory<TPort>>());
    }
}
