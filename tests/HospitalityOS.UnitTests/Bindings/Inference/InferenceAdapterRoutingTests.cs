using HospitalityOS.Adapters.Anthropic;
using HospitalityOS.Adapters.Local;
using HospitalityOS.Bindings;
using HospitalityOS.Ports.Inference;
using HospitalityOS.Ports.Tenancy;
using HospitalityOS.SharedKernel.Results;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace HospitalityOS.UnitTests.Bindings.Inference;

/// <summary>
/// The router over the real adapter registrations, as a host wires them: which provider serves a task is configuration
/// only, startup knows which ports each adapter serves, and an outage falls through to the next provider.
/// </summary>
public sealed class InferenceAdapterRoutingTests
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public void Startup_AcceptsAnthropicForChat_ButNotForEmbeddings_WhichItDoesNotServe()
    {
        Dictionary<string, string?> settings = new()
        {
            ["Inference:Providers:anthropic:Adapter"] = "Anthropic",
            ["Inference:Providers:anthropic:Settings:ApiKeyVariable"] = "ANTHROPIC_API_KEY",
            ["Inference:Tasks:concierge.answer:Provider"] = "anthropic",
            ["Inference:Tasks:concierge.answer:Model"] = "claude-sonnet-5-5",
            ["Inference:Tasks:content.embed:Provider"] = "anthropic",
            ["Inference:Tasks:content.embed:Model"] = "claude-sonnet-5-5",
        };
        using ServiceProvider services = Build(settings);

        OptionsValidationException exception = Assert.Throws<OptionsValidationException>(() => services.GetRequiredService<IStartupValidator>().Validate());

        string failure = Assert.Single(exception.Failures);
        Assert.Contains("'Inference:Tasks:content.embed' uses provider 'anthropic', whose adapter 'Anthropic' does not serve IEmbeddingModel", failure, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Run_WhenTheConfiguredProviderIsDown_FallsBackToTheNext_ByConfigurationAlone()
    {
        Dictionary<string, string?> settings = new()
        {
            ["Inference:Providers:primary:Adapter"] = "Fake",
            ["Inference:Providers:primary:Settings:Unavailable"] = "true",
            ["Inference:Providers:backup:Adapter"] = "Fake",
            ["Inference:Tasks:concierge.answer:Provider"] = "primary",
            ["Inference:Tasks:concierge.answer:Model"] = "fake-large",
            ["Inference:Tasks:concierge.answer:Fallback:0"] = "backup:fake-small",
            ["Inference:Tasks:content.embed:Provider"] = "backup",
            ["Inference:Tasks:content.embed:Model"] = "fake-embed",
        };
        using ServiceProvider services = Build(settings);
        services.GetRequiredService<IStartupValidator>().Validate();
        await using AsyncServiceScope scope = services.CreateAsyncScope();

        Result<InferenceOutcome<ChatCompletion>> answered = await scope.ServiceProvider.GetRequiredService<IInferenceRouter>().RunAsync<IChatModel, ChatCompletion>(
            "concierge.answer", (chat, model, _, token) => chat.CompleteAsync(new ChatRequest(null, [ChatMessage.User("Hello")]), model, token), Cancellation);

        Assert.Equal(["primary", "backup"], answered.Value.Attempts.Select(attempt => attempt.Provider));
        Assert.Equal(("fake-small", "Hello! You said: Hello"), (answered.Value.Value.Model, answered.Value.Value.Message.Text));
    }

    private static ServiceProvider Build(Dictionary<string, string?> settings)
    {
        IConfiguration configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();

        return new ServiceCollection()
            .AddScoped<TenantContext>()
            .AddInference(configuration)
            .AddInferenceTask<IChatModel>("concierge.answer")
            .AddInferenceTask<IEmbeddingModel>("content.embed")
            .AddLocalAdapters()
            .AddAnthropicAdapters()
            .BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
    }
}
