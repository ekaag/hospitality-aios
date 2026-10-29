using System.Text.Json;
using HospitalityOS.Ai;
using HospitalityOS.Ai.Grounding;
using HospitalityOS.Capabilities.Registry;
using HospitalityOS.Contracts.Capabilities;
using HospitalityOS.Contracts.Serialization;
using HospitalityOS.Ports.Inference;
using HospitalityOS.SharedKernel.Execution;
using HospitalityOS.SharedKernel.Ids;
using HospitalityOS.SharedKernel.Results;
using HospitalityOS.UnitTests.Ai.TestDoubles;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;
using static HospitalityOS.UnitTests.Ai.TestDoubles.AiTestHost;

namespace HospitalityOS.UnitTests.Ai.Grounding;

public sealed class GroundedAnswererTests
{
    private const string NoSource = "I don't have confirmed information about that; please ask the front desk.";
    private const string HoursArguments = """{"amenity":"pool"}""";

    private static readonly PropertyId auroraGrand = new(Guid.Parse("01890a5d-ac96-774b-bcce-0000000a0001"));
    private static readonly SourceReference propertySource = new("Property", auroraGrand.Value, 3);
    private static readonly SourceReference wifiSource = new("ContentItem", Guid.Parse("01890a5d-ac96-774b-bcce-0000000c0001"), 2);
    private static readonly SourceReference hoursSource = new("OperatingHours", Guid.Parse("01890a5d-ac96-774b-bcce-0000000b0001"), 1);
    private static readonly SourceReference invented = new("OperatingHours", Guid.Parse("01890a5d-ac96-774b-bcce-0000000dead1"), 1);

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Answer_CitingTheContext_IsAnswered_WithTheContextBuiltStructuredFirst()
    {
        ScriptedModel model = new ScriptedModel().Then(ScriptedModel.Answer(Reply("Answered", "Join the Aurora-Guest network.", wifiSource)));
        ExecutorScript executor = Context();
        using ServiceProvider services = Build(model, executor, _ => { });

        GroundedAnswer answer = await AnswerAsync(services, "What is the wifi?");

        Assert.Equal((GroundedOutcome.Answered, "Join the Aurora-Guest network.", null), (answer.Outcome, answer.Text, answer.Rejection));
        Assert.Equal([wifiSource], answer.Citations);
        Assert.Equal([propertySource, wifiSource], answer.Retrieved);
        Assert.Equal(
            [("Property.GetProperty", (int?)1, $$"""{"propertyId":"{{auroraGrand.Value}}"}"""), ("Property.SearchContent", 1, $$"""{"propertyId":"{{auroraGrand.Value}}","query":"What is the wifi?","limit":3}""")],
            executor.Calls.Select(call => (call.Request.Name, call.Request.Version, call.Request.Input.GetRawText())));
        Assert.All(executor.Calls, call => Assert.Equal(Actor.Agent(Grounded), call.Actor));
        Assert.Equal(["Property.GetProperty@1", "Property.SearchContent@1"], answer.Context.Select(read => read.Capability));
        ChatRequest request = Assert.Single(model.ChatRequests);
        Assert.Contains($$"""Context:{{"\n"}}{"property":{"source":{"type":"Property","id":"{{auroraGrand.Value}}","version":3},"name":"Aurora Grand"},"content":[{"source":""", request.SystemPrompt, StringComparison.Ordinal);
        Assert.Equal(ContractSchemas.For(typeof(GroundedReply)).GetRawText(), request.ResponseSchema?.GetRawText());
    }

    [Fact]
    public async Task Answer_CitingWhatAToolReturned_IsAnswered()
    {
        ScriptedModel model = new ScriptedModel()
            .Then(ScriptedModel.Calls(("call-1", "Test_GetHours", HoursArguments)))
            .Then(ScriptedModel.Answer(Reply("Answered", "The pool closes at 22:00.", hoursSource)));
        ExecutorScript executor = Context();
        executor.Results.Enqueue(JsonSerializer.SerializeToElement(new { hours = new { source = Source(hoursSource), closes = "22:00" } }));
        using ServiceProvider services = Build(model, executor, _ => { });

        GroundedAnswer answer = await AnswerAsync(services, "When does the pool close?");

        Assert.Equal((GroundedOutcome.Answered, "The pool closes at 22:00."), (answer.Outcome, answer.Text));
        Assert.Equal([hoursSource], answer.Citations);
        Assert.Contains(hoursSource, answer.Retrieved);
        Assert.Equal("Test.GetHours@1", Assert.Single(answer.ToolCalls).Capability);
    }

    [Fact]
    public async Task Answer_CitingASourceNobodyRetrieved_IsSentBackOnceWithTheReason_AndTheCorrectionIsAccepted()
    {
        ScriptedModel model = new ScriptedModel()
            .Then(ScriptedModel.Answer(Reply("Answered", "The pool closes at 23:00.", invented)))
            .Then(ScriptedModel.Answer(Reply("Answered", "The Aurora Grand is in London.", propertySource)));
        using ServiceProvider services = Build(model, Context(), _ => { });

        GroundedAnswer answer = await AnswerAsync(services, "Where is the hotel?");

        Assert.Equal((GroundedOutcome.Answered, "The Aurora Grand is in London."), (answer.Outcome, answer.Text));
        ChatMessage correction = model.ChatRequests[1].Messages[^1];
        Assert.Equal(ChatRole.User, correction.Role);
        Assert.StartsWith(
            $"Your reply was not accepted: It cites OperatingHours {invented.Id} version 1, which is not among the sources given or retrieved for this question.",
            correction.Text,
            StringComparison.Ordinal);
        Assert.Equal(model.ChatRequests[1].Messages[^2], answer.Added[0]);
        Assert.Equal([answer.Added[0], correction, answer.Added[2]], answer.Added);
    }

    [Theory]
    [InlineData("invented", "It cites OperatingHours")]
    [InlineData("uncited", "An answer must cite at least one source it relies on.")]
    [InlineData("empty", "The answer is empty.")]
    [InlineData("prose", "The reply is not JSON matching the schema")]
    [InlineData("unknown status", "The reply is not JSON matching the schema")]
    [InlineData("extra member", "The reply is not JSON matching the schema")]
    public async Task Answer_UngroundedTwice_HasNoAuthoritativeSource_AndTellsTheGuestTheCallersAnswer(string kind, string reason)
    {
        string reply = kind switch
        {
            "invented" => Reply("Answered", "Helicopter transfers are free.", invented),
            "uncited" => Reply("Answered", "Helicopter transfers are free."),
            "empty" => Reply("Answered", " ", propertySource),
            "prose" => "Helicopter transfers are free.",
            "extra member" => JsonSerializer.Serialize(new { status = "Answered", answer = "Free.", citations = new[] { Source(propertySource) }, confidence = 0.9 }),
            _ => Reply("Probably", "Helicopter transfers are free.", propertySource),
        };
        ScriptedModel model = new ScriptedModel().Then(ScriptedModel.Answer(reply)).Then(ScriptedModel.Answer(reply)).Then(ScriptedModel.Answer(reply));
        using ServiceProvider services = Build(model, Context(), _ => { });

        GroundedAnswer answer = await AnswerAsync(services, "Do you offer free helicopter transfers?");

        Assert.Equal((GroundedOutcome.NoAuthoritativeSource, NoSource), (answer.Outcome, answer.Text));
        Assert.Empty(answer.Citations);
        Assert.StartsWith(reason, answer.Rejection, StringComparison.Ordinal);
        Assert.Equal(2, model.ChatRequests.Count);
    }

    [Fact]
    public async Task Answer_TheSourcesDoNotClearlySettleIt_IsUncertain_AndTheGuestGetsTheCitedHedgedAnswer()
    {
        ScriptedModel model = new ScriptedModel().Then(ScriptedModel.Answer(Reply("Uncertain", "Our wifi note does not say whether it reaches the garden.", wifiSource)));
        using ServiceProvider services = Build(model, Context(), _ => { });

        GroundedAnswer answer = await AnswerAsync(services, "Does the wifi reach the garden?");

        Assert.Equal(
            (GroundedOutcome.Uncertain, "Our wifi note does not say whether it reaches the garden.", null),
            (answer.Outcome, answer.Text, answer.Rejection));
        Assert.Equal([wifiSource], answer.Citations);
        Assert.Single(model.ChatRequests);
    }

    [Theory]
    [InlineData("uncited", "An answer must cite at least one source it relies on.")]
    [InlineData("invented", "It cites OperatingHours")]
    public async Task Answer_AnUncertainReplyIsGroundedLikeAnAnswer(string kind, string reason)
    {
        string reply = kind == "uncited" ? Reply("Uncertain", "It may reach the garden.") : Reply("Uncertain", "It may reach the garden.", invented);
        ScriptedModel model = new ScriptedModel().Then(ScriptedModel.Answer(reply)).Then(ScriptedModel.Answer(reply));
        using ServiceProvider services = Build(model, Context(), _ => { });

        GroundedAnswer answer = await AnswerAsync(services, "Does the wifi reach the garden?");

        Assert.Equal((GroundedOutcome.NoAuthoritativeSource, NoSource), (answer.Outcome, answer.Text));
        Assert.StartsWith(reason, answer.Rejection, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Answer_TheModelSaysTheSourcesDoNotAnswer_HasNoAuthoritativeSource_WithoutARetry()
    {
        ScriptedModel model = new ScriptedModel().Then(ScriptedModel.Answer(Reply("Unknown", "Free helicopter transfers, I believe.")));
        using ServiceProvider services = Build(model, Context(), _ => { });

        GroundedAnswer answer = await AnswerAsync(services, "Do you offer free helicopter transfers?");

        Assert.Equal((GroundedOutcome.NoAuthoritativeSource, NoSource, null), (answer.Outcome, answer.Text, answer.Rejection));
        Assert.Single(model.ChatRequests);
    }

    [Fact]
    public async Task Answer_ARefusedReply_HasNoAuthoritativeSource_WithoutARetry()
    {
        ScriptedModel model = new ScriptedModel().Then(ScriptedModel.Answer("", ChatStopReason.ContentFiltered));
        using ServiceProvider services = Build(model, Context(), _ => { });

        GroundedAnswer answer = await AnswerAsync(services, "Tell me something unsafe.");

        Assert.Equal((GroundedOutcome.NoAuthoritativeSource, "The reply stopped before it was complete (ContentFiltered)."), (answer.Outcome, answer.Rejection));
        Assert.Single(model.ChatRequests);
    }

    [Fact]
    public async Task Answer_WhenContentSearchFails_StillAnswersFromTheProfileAndTools()
    {
        ScriptedModel model = new ScriptedModel().Then(ScriptedModel.Answer(Reply("Answered", "The Aurora Grand is in London.", propertySource)));
        ExecutorScript executor = new();
        executor.Results.Enqueue(Profile());
        executor.Results.Enqueue(new Error("validation.required", "query is required.", ErrorKind.Validation));
        using ServiceProvider services = Build(model, executor, _ => { });

        GroundedAnswer answer = await AnswerAsync(services, "Where?");

        Assert.Equal(GroundedOutcome.Answered, answer.Outcome);
        Assert.Equal("validation.required", answer.Context[1].ErrorCode);
        Assert.EndsWith("\"content\":[]}\n", model.ChatRequests[0].SystemPrompt, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Answer_ForAnUnknownProperty_IsItsError_WithoutAskingTheModel()
    {
        ScriptedModel model = new();
        ExecutorScript executor = new();
        executor.Results.Enqueue(Error.NotFound("property.not_found", "There is no such property."));
        using ServiceProvider services = Build(model, executor, _ => { });

        Result<GroundedAnswer> result = await AnswerResultAsync(services, Question("Where?"));

        Assert.Equal("property.not_found", result.Error.Code);
        Assert.Empty(model.ChatRequests);
    }

    [Fact]
    public async Task Answer_AnUndeclaredTask_NoGuestMessage_OrACallerContextValue_Throws()
    {
        using ServiceProvider services = Build(new ScriptedModel(), Context(), _ => { });

        await Assert.ThrowsAsync<InvalidOperationException>(() => AnswerResultAsync(services, Question("Where?") with { Task = Concierge }));
        await Assert.ThrowsAsync<ArgumentException>(() => AnswerResultAsync(services, Question("Where?") with { Messages = [ChatMessage.Assistant("Hello.")] }));
        await Assert.ThrowsAsync<ArgumentException>(() => AnswerResultAsync(
            services, Question("Where?") with { PromptValues = new Dictionary<string, string> { ["property"] = "Aurora Grand", ["context"] = "{}" } }));
    }

    [Fact]
    public void Startup_APromptWithoutTheContextPlaceholder_OrMissingContextReads_Fails()
    {
        using ServiceProvider withoutPlaceholder = Build(new ScriptedModel(), new ExecutorScript(), services => services.AddGroundedAnswering(Concierge, "test.concierge", ["Test.GetHours@1"]));
        using ServiceProvider withoutReads = Build(new ScriptedModel(), new ExecutorScript(), services => services.AddSingleton(new CapabilityRegistry(AiTestHost.Capabilities.Take(4))));

        Assert.Contains(
            "Grounded task 'test.concierge' uses prompt 'test.concierge@2', which has no {{context}} placeholder for the context.",
            Assert.Throws<OptionsValidationException>(() => withoutPlaceholder.GetRequiredService<IStartupValidator>().Validate()).Failures);
        Assert.Contains(
            "Grounded answering reads Property.GetProperty@1 for context, which is not registered (AddBuiltInCapabilities).",
            Assert.Throws<AggregateException>(() => withoutReads.GetRequiredService<IStartupValidator>().Validate()).InnerExceptions.OfType<OptionsValidationException>().SelectMany(exception => exception.Failures));
    }

    private static ExecutorScript Context()
    {
        ExecutorScript executor = new();
        executor.Results.Enqueue(Profile());
        executor.Results.Enqueue(JsonSerializer.SerializeToElement(new { results = new[] { new { source = Source(wifiSource), title = "Wi-Fi", body = "Join the Aurora-Guest network." } } }));

        return executor;
    }

    private static JsonElement Profile() => JsonSerializer.SerializeToElement(new { source = Source(propertySource), name = "Aurora Grand" });

    private static object Source(SourceReference source) => new { type = source.Type, id = source.Id, version = source.Version };

    private static string Reply(string status, string answer, params SourceReference[] citations) =>
        JsonSerializer.Serialize(new { status, answer, citations = citations.Select(Source) });

    private static GroundedQuestion Question(string asked) =>
        new(Grounded, auroraGrand, [ChatMessage.User(asked)], NoSource, new Dictionary<string, string> { ["property"] = "Aurora Grand" });

    private static async Task<GroundedAnswer> AnswerAsync(ServiceProvider services, string asked)
    {
        Result<GroundedAnswer> result = await AnswerResultAsync(services, Question(asked));
        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Message : null);

        return result.Value;
    }

    private static async Task<Result<GroundedAnswer>> AnswerResultAsync(ServiceProvider services, GroundedQuestion question)
    {
        await using AsyncServiceScope scope = services.CreateAsyncScope();

        return await scope.ServiceProvider.GetRequiredService<IGroundedAnswerer>().AnswerAsync(question, Cancellation);
    }
}
