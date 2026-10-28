using System.Text.Json;
using HospitalityOS.Adapters.Local;
using HospitalityOS.Adapters.Local.Inference;
using HospitalityOS.Ports.Bindings;
using HospitalityOS.Ports.Inference;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace HospitalityOS.UnitTests.Adapters.Local.Inference;

/// <summary>
/// The Fake adapter's scripts (which answer before its defaults), its simulated outage, and its settings checks.
/// </summary>
public sealed class FakeModelTests : IDisposable
{
    private static readonly ModelSelection fake = new("fake", InferenceParameters.Default);
    private static readonly ChatTool hours = new("Property_GetOperatingHours", "Opening hours.", JsonDocument.Parse("""{"type":"object","properties":{}}""").RootElement.Clone());

    private readonly ServiceProvider services;

    public FakeModelTests()
    {
        FakeScriptOptions concierge = new();
        concierge.Chat.Add(new FakeChatRuleOptions(AfterTool: hours.Name, Reply: "Per our hours: {toolResult}"));
        concierge.Chat.Add(new FakeChatRuleOptions(WhenUserSays: "pool", CallTool: hours.Name, Arguments: """{"subject":"Amenity"}"""));
        concierge.Classification.Add(new FakeClassificationRuleOptions("towel", "service_request"));
        concierge.Extraction.Add(new FakeExtractionRuleOptions("towel", """{"item":"bath_towel","quantity":2}"""));
        concierge.Judgement.Add(new FakeJudgementRuleOptions("invented", 0, false, "Invents a time."));
        FakeScriptOptions broken = new();
        broken.Chat.Add(new FakeChatRuleOptions(WhenUserSays: "hello"));
        broken.Chat.Add(new FakeChatRuleOptions(CallTool: "x", Arguments: "[1]"));
        broken.Extraction.Add(new FakeExtractionRuleOptions("x", "{not json"));

        services = new ServiceCollection()
            .AddLocalAdapters()
            .Configure<FakeInferenceOptions>(options =>
            {
                options.Scripts["concierge"] = concierge;
                options.Scripts["broken"] = broken;
            })
            .BuildServiceProvider();
    }

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    public void Dispose() => services.Dispose();

    [Fact]
    public async Task Chat_FollowsTheScript_CallingTheToolThenAnsweringFromItsResult()
    {
        IChatModel model = Create<IChatModel>(("Script", "concierge"));
        ChatMessage question = ChatMessage.User("When does the POOL close?");

        ChatCompletion asked = await model.CompleteAsync(new ChatRequest(null, [question], [hours]), fake, Cancellation);
        ChatToolCall call = Assert.IsType<ChatToolCall>(Assert.Single(asked.Message.Content));
        ChatCompletion answered = await model.CompleteAsync(
            new ChatRequest(null, [question, asked.Message, new ChatMessage(ChatRole.Tool, [new ChatToolResult(call.CallId, "closes 22:00")])], [hours]),
            fake,
            Cancellation);

        Assert.Equal((ChatStopReason.ToolCalls, hours.Name, "Amenity"), (asked.StopReason, call.Name, call.Arguments.GetProperty("subject").GetString()));
        Assert.Equal((ChatStopReason.Completed, "Per our hours: closes 22:00"), (answered.StopReason, answered.Message.Text));
    }

    [Fact]
    public async Task Chat_WhenNoRuleMatches_UsesTheDefaults()
    {
        ChatCompletion completion = await Create<IChatModel>(("Script", "concierge")).CompleteAsync(new ChatRequest(null, [ChatMessage.User("Hi there")]), fake, Cancellation);

        Assert.Equal("Hello! You said: Hi there", completion.Message.Text);
    }

    [Fact]
    public async Task ClassificationExtractionAndJudgement_FollowTheScript()
    {
        Classification classification = await Create<IClassifier>(("Script", "concierge")).ClassifyAsync(
            new ClassificationRequest("question: asks something", "Two TOWELS please", ["question", "service_request"]), fake, Cancellation);
        Extraction extraction = await Create<IStructuredExtractor>(("Script", "concierge")).ExtractAsync(
            new ExtractionRequest("Extract.", "Two towels please", "request", JsonDocument.Parse("{}").RootElement), fake, Cancellation);
        Judgement judgement = await Create<IEvaluationJudge>(("Script", "concierge")).JudgeAsync(
            new JudgeRequest("No guessing.", "When?", "An invented time", "22:00"), fake, Cancellation);

        Assert.Equal("service_request", classification.Label);
        Assert.Equal(("bath_towel", 2), (extraction.Output.GetProperty("item").GetString(), extraction.Output.GetProperty("quantity").GetInt32()));
        Assert.Equal((false, "Invents a time."), (judgement.Passed, judgement.Reasoning));
    }

    [Fact]
    public async Task Unavailable_FailsEveryCallAsAnOutage()
    {
        await Assert.ThrowsAsync<InferenceUnavailableException>(() => Create<IChatModel>(("Unavailable", "true"))
            .CompleteAsync(new ChatRequest(null, [ChatMessage.User("Hi")]), fake, Cancellation));
        await Assert.ThrowsAsync<InferenceUnavailableException>(() => Create<IEmbeddingModel>(("Unavailable", "true"))
            .EmbedAsync(new EmbeddingRequest(["pool"]), fake, Cancellation));
    }

    [Fact]
    public void ValidateSettings_RefusesAnUnknownScript_ABrokenScript_AndABadUnavailableValue()
    {
        IAdapterFactory<IChatModel> factory = Factory<IChatModel>();

        Assert.Empty(factory.ValidateSettings(Settings(("Script", "concierge"), ("Unavailable", "false"))));
        Assert.Contains(factory.ValidateSettings(Settings(("Script", "spa"))), problem => problem.StartsWith("Script 'spa' is not registered. Registered: broken, concierge", StringComparison.Ordinal));
        Assert.Contains(factory.ValidateSettings(Settings(("Unavailable", "sometimes"))), problem => problem.Contains("must be true or false", StringComparison.Ordinal));
        Assert.Equal(
            [
                "Script 'broken' chat rule 0 must either reply or call a tool.",
                "Script 'broken' chat rule 1 arguments must be a JSON object.",
                "Script 'broken' extraction rule 0 output must be JSON.",
            ],
            factory.ValidateSettings(Settings(("Script", "broken"))));
    }

    private TPort Create<TPort>(params (string Key, string Value)[] settings)
        where TPort : class => Factory<TPort>().Create(services, Settings(settings));

    private IAdapterFactory<TPort> Factory<TPort>()
        where TPort : class => services.GetServices<IAdapterFactory<TPort>>().Single(factory => factory.AdapterName == "Fake");

    private static IConfiguration Settings(params (string Key, string Value)[] values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values.Select(value => new KeyValuePair<string, string?>(value.Key, value.Value))).Build();
}
