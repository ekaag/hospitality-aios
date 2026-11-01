using HospitalityOS.Adapters.Anthropic;
using HospitalityOS.Adapters.AzureOpenAI;
using HospitalityOS.Adapters.Local;
using HospitalityOS.Ports.Bindings;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace HospitalityOS.PortContractTests.Inference;

/// <summary>
/// Creates inference adapters the way the router does: through the adapter project's registered factory, with
/// provider settings.
/// </summary>
internal static class InferenceAdapters
{
    private const string ApiKeyVariable = "ANTHROPIC_API_KEY";
    private const string EndpointVariable = "AZURE_OPENAI_ENDPOINT";

    private static readonly ServiceProvider services = new ServiceCollection().AddLocalAdapters().AddAnthropicAdapters().AddAzureOpenAIAdapters().BuildServiceProvider();

    public static TPort Fake<TPort>()
        where TPort : class => Create<TPort>("Fake", []);

    /// <summary>
    /// The live Anthropic adapter. Skips the test unless <c>ANTHROPIC_API_KEY</c> is set, because each run calls Claude
    /// (a few cents).
    /// </summary>
    public static TPort Anthropic<TPort>()
        where TPort : class
    {
        Assert.SkipUnless(!string.IsNullOrEmpty(Environment.GetEnvironmentVariable(ApiKeyVariable)), $"Set {ApiKeyVariable} to run the live Anthropic contract tests.");

        return Create<TPort>("Anthropic", new Dictionary<string, string?> { ["ApiKeyVariable"] = ApiKeyVariable });
    }

    /// <summary>
    /// The live Azure OpenAI adapter, against the resource <c>AZURE_OPENAI_ENDPOINT</c> names, signed in as whoever the
    /// Azure CLI is signed in as (who needs Cognitive Services OpenAI User on it). Skips the test unless it is set; the
    /// resource must hold the deployments the inference modules make (<c>gpt-5-mini</c>, <c>text-embedding-3-small</c>).
    /// </summary>
    public static TPort AzureOpenAI<TPort>()
        where TPort : class
    {
        string? endpoint = Environment.GetEnvironmentVariable(EndpointVariable);
        Assert.SkipUnless(!string.IsNullOrEmpty(endpoint), $"Set {EndpointVariable} to run the live Azure OpenAI contract tests.");

        return Create<TPort>("AzureOpenAI", new Dictionary<string, string?> { ["Endpoint"] = endpoint });
    }

    private static TPort Create<TPort>(string adapter, Dictionary<string, string?> settings)
        where TPort : class =>
        services.GetServices<IAdapterFactory<TPort>>()
            .Single(factory => factory.AdapterName == adapter)
            .Create(services, new ConfigurationBuilder().AddInMemoryCollection(settings).Build());
}
