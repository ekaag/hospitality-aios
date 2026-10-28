using HospitalityOS.Bindings;
using HospitalityOS.Bindings.Inference;
using HospitalityOS.Ports.Inference;
using HospitalityOS.UnitTests.Bindings.TestDoubles;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;
using static HospitalityOS.UnitTests.Bindings.Inference.TestDoubles.InferenceTestHost;

namespace HospitalityOS.UnitTests.Bindings.Inference;

public sealed class InferenceStartupValidatorTests
{
    private static readonly string[] toUnknown = ["missing:any-model"];
    private static readonly string[] malformed = ["no-model-here"];

    [Fact]
    public void Validate_WithEveryDeclaredTaskRoutedToProvidersServingIt_Succeeds()
    {
        using ServiceProvider services = Build(Settings(
            Provider("primary"),
            Provider("chat-only", "ChatOnly"),
            Route(Answer, "primary", "big-model", ["chat-only:backup"]),
            Route(Classify, "primary", "small-model"),
            Route(Answer, "chat-only", "tenant-model", tenant: "aa000000-0000-4000-8000-000000000001")));

        Assert.Null(Record.Exception(() => Validate(services)));
    }

    [Fact]
    public void Validate_WithNoInferenceConfigured_AndNoTasksDeclared_Succeeds()
    {
        using ServiceProvider services = Build(new Dictionary<string, string?>(), _ => { });

        Assert.Null(Record.Exception(() => Validate(services)));
    }

    [Fact]
    public void Validate_ADeclaredTaskWithoutARoute_Fails() =>
        AssertFails(Settings(Provider("primary"), Route(Answer, "primary", "big-model")), "Inference task 'intent.classify' has no route");

    [Fact]
    public void Validate_ARouteForATaskTheCodeDoesNotDeclare_Fails() =>
        AssertFails(Routed(Route("concierge.anwser", "primary", "big-model")), "routes inference task 'concierge.anwser', which the code does not declare");

    [Fact]
    public void Validate_AnUnknownProvider_InAFallbackOrTenantOverride_Fails()
    {
        AssertFails(Routed(Route(Answer, "primary", "big-model", toUnknown)), "uses provider 'missing', which is not configured");
        AssertFails(Routed(Route(Answer, "missing", "tenant-model", tenant: "aa000000-0000-4000-8000-000000000001")), "uses provider 'missing', which is not configured");
    }

    [Fact]
    public void Validate_AProviderWhoseAdapterDoesNotServeTheTasksPort_Fails() =>
        AssertFails(
            Settings(Provider("primary"), Provider("chat-only", "ChatOnly"), Route(Answer, "primary", "big-model"), Route(Classify, "chat-only", "small-model")),
            "whose adapter 'ChatOnly' does not serve IClassifier");

    [Fact]
    public void Validate_AProviderWithAnUnknownAdapter_NoAdapter_OrInvalidSettings_Fails()
    {
        AssertFails(Routed(Provider("spare", "Anthropic")), "selects unknown inference adapter 'Anthropic'");
        AssertFails(Routed([new("Inference:Providers:spare:Settings:Label", "spare")]), "must set 'Adapter'");
        AssertFails(Routed(Provider("spare", "Test", ("Behavior", "flaky"))), "Settings': Behavior 'flaky' is not one of");
    }

    [Theory]
    [InlineData("Inference:Tasks:concierge.answer:Parameters:Effort", "extreme", "must be one of Low, Medium, High, Max")]
    [InlineData("Inference:Tasks:concierge.answer:Parameters:Effort", "1", "must be one of Low, Medium, High, Max")]
    [InlineData("Inference:Tasks:concierge.answer:Parameters:Temperature", "0.2", "is not an inference parameter; use MaxOutputTokens or Effort")]
    [InlineData("Inference:Tasks:concierge.answer:Parameters:MaxOutputTokens", "0", "must be a whole number of at least 1")]
    [InlineData("Inference:Tasks:concierge.answer:Parameters:MaxOutputTokens", "12.5", "must be a whole number of at least 1")]
    public void Validate_AnInvalidParameter_Fails(string key, string value, string expected) =>
        AssertFails(Routed([new(key, value)]), expected);

    [Fact]
    public void Validate_ARouteWithoutAModel_OrAMalformedFallback_Fails()
    {
        AssertFails(
            Settings(Provider("primary"), Route(Classify, "primary", "small-model"), [new("Inference:Tasks:concierge.answer:Provider", "primary")]),
            "'Inference:Tasks:concierge.answer' must set 'Provider' and 'Model'");
        AssertFails(
            Settings(Provider("primary"), Route(Answer, "primary", "big-model", malformed), Route(Classify, "primary", "small-model")),
            "'Inference:Tasks:concierge.answer:Fallback:0' must be 'provider:model', not 'no-model-here'");
    }

    [Fact]
    public void Validate_ATaskDeclaredForTwoPorts_OrForAPortThatIsNotForInference_Fails()
    {
        AssertFails(Routed(), "declared for more than one port: IChatModel, IClassifier", services =>
        {
            services.AddInferenceTask<IChatModel>(Answer);
            services.AddInferenceTask<IClassifier>(Answer);
            services.AddInferenceTask<IClassifier>(Classify);
        });
        AssertFails(Routed(), "is declared for IGreeter, which is not an inference port", services =>
        {
            services.AddInferenceTask<IChatModel>(Answer);
            services.AddInferenceTask<IClassifier>(Classify);
            services.AddInferenceTask<IGreeter>("greeting.compose");
        });
    }

    [Fact]
    public void Validate_ATaskWithAnEmbeddedPrompt_AndRoutesPinningVersionsOfIt_Succeeds()
    {
        using ServiceProvider services = Build(
            Routed([new("Inference:Tasks:concierge.answer:PromptVersion", "1"), new("Inference:Tenants:aa000000-0000-4000-8000-000000000001:Tasks:concierge.answer:Provider", "primary"),
                new("Inference:Tenants:aa000000-0000-4000-8000-000000000001:Tasks:concierge.answer:Model", "big-model"),
                new("Inference:Tenants:aa000000-0000-4000-8000-000000000001:Tasks:concierge.answer:PromptVersion", "2")]),
            WithPrompt("test.concierge"));

        Assert.Null(Record.Exception(() => Validate(services)));
    }

    [Fact]
    public void Validate_ATasksPromptThatIsNotEmbedded_Fails() =>
        AssertFails(Routed(), "Inference task 'concierge.answer' uses prompt 'concierge.answer', which is not embedded in any assembly registered with AddPrompts", WithPrompt("concierge.answer"));

    [Fact]
    public void Validate_APinnedPromptVersionThatDoesNotExist_Fails() =>
        AssertFails(
            Routed([new("Inference:Tasks:concierge.answer:PromptVersion", "3")]),
            "'Inference:Tasks:concierge.answer:PromptVersion' pins prompt 'test.concierge@3', which does not exist (the latest is test.concierge@2)",
            WithPrompt("test.concierge"));

    [Fact]
    public void Validate_APinnedPromptVersionForATaskWithoutAPrompt_Fails() =>
        AssertFails(
            Routed([new("Inference:Tasks:intent.classify:PromptVersion", "1")]),
            "'Inference:Tasks:intent.classify:PromptVersion' pins a prompt version, but inference task 'intent.classify' declares no prompt",
            WithPrompt("test.concierge"));

    [Theory]
    [InlineData("0")]
    [InlineData("two")]
    [InlineData("1.5")]
    public void Validate_APromptVersionThatIsNotAPositiveWholeNumber_Fails(string value) =>
        AssertFails(
            Routed([new("Inference:Tasks:concierge.answer:PromptVersion", value)]),
            $"'Inference:Tasks:concierge.answer:PromptVersion' must be a prompt version number of at least 1, not '{value}'",
            WithPrompt("test.concierge"));

    [Fact]
    public void Validate_ATaskDeclaredWithTwoPrompts_Fails() =>
        AssertFails(Routed(), "Inference task 'concierge.answer' is declared with more than one prompt: test.concierge, (none)", services =>
        {
            WithPrompt("test.concierge")(services);
            services.AddInferenceTask<IChatModel>(Answer);
        });

    [Fact]
    public void Validate_AnUnlockedPrompt_FailsStartup()
    {
        AssertFails(Routed(), "Prompt 'draft.answer@1' in Drafts is not locked", services =>
        {
            WithPrompt("test.concierge")(services);
            services.AddSingleton(new PromptSource("Drafts", [new PromptFile("draft.answer/v1.md", "Draft.")], null));
        });
    }

    // concierge.answer declared with the given prompt, intent.classify without one; the test assembly's prompts registered.
    private static Action<IServiceCollection> WithPrompt(string prompt) => services =>
    {
        services.AddPrompts(typeof(InferenceStartupValidatorTests).Assembly);
        services.AddInferenceTask<IChatModel>(Answer, prompt);
        services.AddInferenceTask<IClassifier>(Classify);
    };

    // A valid base configuration (both default tasks routed to "primary") with extra settings.
    private static Dictionary<string, string?> Routed(params IEnumerable<KeyValuePair<string, string?>> extra) =>
        Settings(Provider("primary"), Route(Answer, "primary", "big-model"), Route(Classify, "primary", "small-model"), extra);

    private static void AssertFails(Dictionary<string, string?> settings, string expected, Action<IServiceCollection>? declare = null)
    {
        using ServiceProvider services = Build(settings, declare);

        OptionsValidationException exception = Assert.Throws<OptionsValidationException>(() => Validate(services));

        Assert.Contains(exception.Failures, failure => failure.Contains(expected, StringComparison.Ordinal));
    }

    private static void Validate(ServiceProvider services) => services.GetRequiredService<IStartupValidator>().Validate();
}
