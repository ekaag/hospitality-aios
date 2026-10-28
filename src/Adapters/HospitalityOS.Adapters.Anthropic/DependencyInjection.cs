using HospitalityOS.Ports.Bindings;
using HospitalityOS.Ports.Inference;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace HospitalityOS.Adapters.Anthropic;

/// <summary>
/// Registers the Anthropic (Claude) inference adapter so configuration can bind providers to it.
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Registers <c>Anthropic</c> for <see cref="IChatModel"/>, <see cref="IStructuredExtractor"/>, <see cref="IClassifier"/>,
    /// and <see cref="IEvaluationJudge"/>. Anthropic offers no embedding or reranking models, so those ports need another
    /// provider. Provider settings: <c>ApiKeyVariable</c>, <c>BaseUrl</c>, <c>MaxRetries</c>, <c>TimeoutSeconds</c>,
    /// <c>RefusalFallback</c> (<c>Default</c> or <c>Off</c>).
    /// </summary>
    public static IServiceCollection AddAnthropicAdapters(this IServiceCollection services)
    {
        Add<IChatModel>(services);
        Add<IStructuredExtractor>(services);
        Add<IClassifier>(services);
        Add<IEvaluationJudge>(services);

        return services;
    }

    private static void Add<TPort>(IServiceCollection services)
        where TPort : class
    {
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IAdapterFactory<TPort>, AnthropicModelFactory<TPort>>());
    }
}
