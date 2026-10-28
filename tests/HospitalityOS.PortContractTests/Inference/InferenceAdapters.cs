using HospitalityOS.Adapters.Anthropic;
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

    private static readonly ServiceProvider services = new ServiceCollection().AddLocalAdapters().AddAnthropicAdapters().BuildServiceProvider();

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

    private static TPort Create<TPort>(string adapter, Dictionary<string, string?> settings)
        where TPort : class =>
        services.GetServices<IAdapterFactory<TPort>>()
            .Single(factory => factory.AdapterName == adapter)
            .Create(services, new ConfigurationBuilder().AddInMemoryCollection(settings).Build());
}
