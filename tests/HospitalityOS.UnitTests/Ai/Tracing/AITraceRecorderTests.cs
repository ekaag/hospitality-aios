using System.Text.Json;
using HospitalityOS.Ai;
using HospitalityOS.Ai.Grounding;
using HospitalityOS.Ai.StructuredOutput;
using HospitalityOS.Ai.ToolLoops;
using HospitalityOS.Ai.Tracing;
using HospitalityOS.Contracts.Api.Ai;
using HospitalityOS.Contracts.Capabilities;
using HospitalityOS.Contracts.Serialization;
using HospitalityOS.Domain.Capabilities.AITraces;
using HospitalityOS.Domain.Capabilities.AITraces.Events;
using HospitalityOS.Ports.Inference;
using HospitalityOS.Ports.Tenancy;
using HospitalityOS.SharedKernel.Execution;
using HospitalityOS.SharedKernel.Ids;
using HospitalityOS.SharedKernel.Results;
using HospitalityOS.SharedKernel.Tenancy;
using HospitalityOS.UnitTests.Ai.StructuredOutput;
using HospitalityOS.UnitTests.Ai.TestDoubles;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Xunit;
using static HospitalityOS.UnitTests.Ai.TestDoubles.AiTestHost;
using AITraceOutcome = HospitalityOS.Domain.Capabilities.AITraces.AITraceOutcome;

namespace HospitalityOS.UnitTests.Ai.Tracing;

public sealed class AITraceRecorderTests
{
    private const string NoSource = "I don't have confirmed information about that.";
    private const string HoursArguments = """{"amenity":"pool"}""";
    private const string TowelsJson = """{"item":"towel","quantity":2,"urgency":"Normal","room":"412"}""";

    private static readonly TenantId tenant = new(Guid.Parse("01890a5d-ac96-774b-bcce-0000000e0001"));
    private static readonly PropertyId auroraGrand = new(Guid.Parse("01890a5d-ac96-774b-bcce-0000000a0001"));
    private static readonly Guid message = Guid.Parse("01890a5d-ac96-774b-bcce-0000000f0001");
    private static readonly Guid flow = Guid.Parse("01890a5d-ac96-774b-bcce-0000000000ff");
    private static readonly SourceReference propertySource = new("Property", auroraGrand.Value, 3);
    private static readonly SourceReference wifiSource = new("ContentItem", Guid.Parse("01890a5d-ac96-774b-bcce-0000000c0001"), 2);
    private static readonly SourceReference hoursSource = new("OperatingHours", Guid.Parse("01890a5d-ac96-774b-bcce-0000000b0001"), 1);
    private static readonly SourceReference invented = new("OperatingHours", Guid.Parse("01890a5d-ac96-774b-bcce-0000000dead1"), 1);

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    private static Dictionary<string, string> Values => new() { ["property"] = "Aurora Grand" };

    [Fact]
    public async Task Complete_AfterAGroundedAnswerThatUsedATool_RecordsTheWholeTurn()
    {
        FakeTimeProvider? clock = null;
        ScriptedModel model = new ScriptedModel()
            .Then(_ =>
            {
                // The model takes a second to answer: the turn started at its first step, not its last.
                clock!.Advance(TimeSpan.FromSeconds(1));

                return ScriptedModel.Calls(("call-1", "Test_GetHours", HoursArguments));
            })
            .Then(ScriptedModel.Answer(Reply("Answered", "The pool closes at 22:00.", hoursSource)));
        ExecutorScript executor = Context();
        executor.Results.Enqueue(JsonSerializer.SerializeToElement(new { hours = new { source = Source(hoursSource), closes = "22:00" } }));
        using ServiceProvider services = Build(model, executor, _ => { });
        clock = (FakeTimeProvider)services.GetRequiredService<TimeProvider>();

        AITrace trace = await AnswerAndCompleteAsync(services, "When does the pool close?", "question");
        AITraceBody body = BodyOf(trace);

        Assert.Equal(
            (TraceId, (PropertyId?)auroraGrand, Actor.Agent(Grounded), flow, tenant),
            (trace.Id.Value, trace.PropertyId, trace.Actor, trace.CorrelationId, trace.TenantId));
        Assert.Equal(
            (AITraceTrigger.GuestMessage, (Guid?)message, "question", AITraceOutcome.Answered, Now, Now.AddSeconds(3)),
            (trace.TriggerType, trace.TriggerId, trace.Intent, trace.Outcome, trace.StartedAt, trace.CompletedAt));
        Assert.Equal(new AITraceCreated(trace.Id, auroraGrand, Grounded, AITraceTrigger.GuestMessage, message, AITraceOutcome.Answered, Now.AddSeconds(3)), Assert.Single(trace.DomainEvents));

        Assert.Equal("When does the pool close?", body.TriggerText);
        Assert.Equal([propertySource, wifiSource, hoursSource], body.Retrieved);
        Assert.Equal(
            [
                (CapabilityCallPurpose.Context, "Property.GetProperty@1", (string?)null, (Guid?)RecordingExecutor.InvocationFor(1)),
                (CapabilityCallPurpose.Context, "Property.SearchContent@1", null, RecordingExecutor.InvocationFor(2)),
                (CapabilityCallPurpose.Tool, "Test.GetHours@1", "call-1", RecordingExecutor.InvocationFor(3)),
            ],
            body.CapabilityCalls.Select(call => (call.Purpose, call.Capability, call.CallId, call.InvocationId)));
        Assert.All(body.CapabilityCalls, call => Assert.Equal(
            (AuditClassification.Routine, AuthorizationDecision.Allowed, (string?)null, false),
            (call.Classification!.Value, call.Authorization, call.ErrorCode, call.Withheld)));
        Assert.Equal(HoursArguments, body.CapabilityCalls[2].Input?.GetRawText());
        Assert.Contains("\"closes\":\"22:00\"", body.CapabilityCalls[2].Output?.GetRawText(), StringComparison.Ordinal);

        Assert.Equal(2, body.ModelCalls.Count);
        ModelCall first = body.ModelCalls[0];
        ModelAttempt attempt = Assert.Single(first.Attempts);
        Assert.Equal(("scripted", "scripted-model", "test.grounded", 1, (string?)null), (attempt.Provider, attempt.Model, attempt.Prompt?.Name, attempt.Prompt?.Number ?? 0, attempt.Failure));
        Assert.StartsWith("sha256:", attempt.Prompt!.Hash, StringComparison.Ordinal);
        Assert.Equal(model.ChatRequests[0].SystemPrompt, first.SystemPrompt);
        Assert.Equal(
            (ModelCallKind.Chat, Grounded, false, true, (TraceStopReason?)TraceStopReason.ToolCalls, (TokenUsage?)new TokenUsage(10, 5), "scripted-model"),
            (first.Kind, first.Task, first.ContinuesConversation, first.StructuredOutput, first.StopReason, first.Usage, first.Model));
        Assert.Equal(["Test_GetHours"], first.Tools);
        Assert.Equal([new TraceMessage(TraceMessageRole.User, [new TracePart(TracePartType.Text, Text: "When does the pool close?")])], first.Input);
        TracePart call = Assert.Single(first.Output!.Parts);
        Assert.Equal((TracePartType.ToolCall, "call-1", "Test_GetHours", HoursArguments), (call.Type, call.CallId, call.Name, call.Arguments?.GetRawText()));

        // The second call resends the conversation; only the tool results are new.
        ModelCall second = body.ModelCalls[1];
        Assert.True(second.ContinuesConversation);
        TraceMessage results = Assert.Single(second.Input);
        Assert.Equal((TraceMessageRole.Tool, TracePartType.ToolResult, "call-1", (bool?)false), (results.Role, results.Parts[0].Type, results.Parts[0].CallId, results.Parts[0].IsError));
        Assert.Equal((TraceStopReason?)TraceStopReason.Completed, second.StopReason);
        Assert.Equal(Reply("Answered", "The pool closes at 22:00.", hoursSource), second.Output!.Parts[0].Text);

        Assert.Equal([new ReplyVerdict(true, null)], body.Grounding!.Replies);
        Assert.Equal(new TraceResponse("The pool closes at 22:00.", [hoursSource]), body.Response);
        Assert.Equal(new TokenUsage(20, 10), body.Usage);
    }

    [Fact]
    public async Task Complete_AfterAnUngroundedAnswer_RecordsEachRejection_AndThatNoAuthoritativeSourceWasFound()
    {
        string reply = Reply("Answered", "Helicopter transfers are free.", invented);
        ScriptedModel model = new ScriptedModel().Then(ScriptedModel.Answer(reply)).Then(ScriptedModel.Answer(reply));
        using ServiceProvider services = Build(model, Context(), _ => { });

        AITrace trace = await AnswerAndCompleteAsync(services, "Do you offer free helicopter transfers?");
        AITraceBody body = BodyOf(trace);

        Assert.Equal(AITraceOutcome.NoAuthoritativeSource, trace.Outcome);
        Assert.Equal(new TraceResponse(NoSource, []), body.Response);
        Assert.Equal([propertySource, wifiSource], body.Retrieved);
        Assert.Equal(2, body.Grounding!.Replies.Count);
        Assert.All(body.Grounding.Replies, verdict =>
        {
            Assert.False(verdict.Accepted);
            Assert.StartsWith($"It cites OperatingHours {invented.Id} version 1", verdict.Rejection, StringComparison.Ordinal);
        });

        // The retry resends the rejected reply (already recorded as the first call's output) and the correction.
        ModelCall retry = body.ModelCalls[1];
        Assert.True(retry.ContinuesConversation);
        Assert.StartsWith("Your reply was not accepted:", Assert.Single(retry.Input).Parts[0].Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Complete_WhenNoModelCouldServe_RecordsTheFailedAttempts()
    {
        ScriptedModel model = new ScriptedModel().Then(_ => throw new InferenceUnavailableException("overloaded"));
        using ServiceProvider services = Build(model, Context(), _ => { });
        await using AsyncServiceScope scope = services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().Set(tenant);

        Result<GroundedAnswer> answer = await scope.ServiceProvider.GetRequiredService<IGroundedAnswerer>()
            .AnswerAsync(new GroundedQuestion(Grounded, auroraGrand, [ChatMessage.User("Where?")], NoSource, Values), Cancellation);
        await scope.ServiceProvider.GetRequiredService<IAITraceRecorder>()
            .CompleteAsync(new AITraceCompletion(Trigger("Where?"), AITraceOutcome.Failed, NoSource, [], auroraGrand), Cancellation);

        Assert.Equal(InferenceUnavailableError.UnavailableCode, answer.Error.Code);
        AITrace trace = Assert.Single(services.GetRequiredService<TraceStore>().Added);
        ModelCall failed = Assert.Single(BodyOf(trace).ModelCalls);
        Assert.Equal(((TraceMessage?)null, (TraceStopReason?)null, (string?)null), (failed.Output, failed.StopReason, failed.Model));
        Assert.Equal("overloaded", Assert.Single(failed.Attempts).Failure);
        Assert.NotNull(failed.SystemPrompt);
        Assert.Equal(AITraceOutcome.Failed, trace.Outcome);
    }

    [Theory]
    [InlineData("capability.forbidden", AuthorizationDecision.Denied)]
    [InlineData("capability.confirmation_required", AuthorizationDecision.ConfirmationRequired)]
    [InlineData("capability.unknown", AuthorizationDecision.NotEvaluated)]
    [InlineData("capability.invalid_input", AuthorizationDecision.Allowed)]
    public async Task Complete_RecordsEachToolCallsAuthorizationDecision(string error, AuthorizationDecision decision)
    {
        ScriptedModel model = new ScriptedModel()
            .Then(ScriptedModel.Calls(("call-1", "Test_GetHours", HoursArguments)))
            .Then(ScriptedModel.Answer("Sorry."));
        ExecutorScript executor = new();
        executor.Results.Enqueue(new Error(error, "No.", ErrorKind.Forbidden));
        using ServiceProvider services = Build(model, executor, collection => collection.AddToolLoop(Concierge, "test.concierge", ["Test.GetHours@1"]));

        CapabilityCall call = Assert.Single(BodyOf(await RunLoopAsync(services)).CapabilityCalls);

        Assert.Equal((decision, error, (Guid?)null, (JsonElement?)null), (call.Authorization, call.ErrorCode, call.InvocationId, call.Output));
    }

    [Fact]
    public async Task Complete_RecordsAToolThatIsNotAllowListed_AsNotOnTheAllowList()
    {
        ScriptedModel model = new ScriptedModel()
            .Then(ScriptedModel.Calls(("call-1", "Test_BookTable", "{}")))
            .Then(ScriptedModel.Answer("Sorry."));
        using ServiceProvider services = Build(model, new ExecutorScript(), collection => collection.AddToolLoop(Concierge, "test.concierge", ["Test.GetHours@1"]));

        CapabilityCall call = Assert.Single(BodyOf(await RunLoopAsync(services)).CapabilityCalls);

        Assert.Equal(
            ("Test_BookTable", (AuditClassification?)null, AuthorizationDecision.NotOnAllowList, (string?)ToolLoop.UnavailableToolCode),
            (call.Capability, call.Classification, call.Authorization, call.ErrorCode));
    }

    [Fact]
    public async Task Complete_RecordsEachExtractionCall_WithItsInstructionsInputAndOutput()
    {
        ScriptedModel model = new ScriptedModel().ThenExtract("""{"item":"towel"}""").ThenExtract(TowelsJson);
        using ServiceProvider services = Build(model, new ExecutorScript(), _ => { });
        await using AsyncServiceScope scope = services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().Set(tenant);

        await scope.ServiceProvider.GetRequiredService<ITypedExtractor>()
            .ExtractAsync<TypedExtractorTests.ItemRequest>(Extract, "Two towels to 412, please.", new Dictionary<string, string> { ["property"] = "Aurora Grand" }, Cancellation);
        await scope.ServiceProvider.GetRequiredService<IAITraceRecorder>()
            .CompleteAsync(new AITraceCompletion(Trigger("Two towels to 412, please."), AITraceOutcome.Answered, null, []), Cancellation);

        AITraceBody body = BodyOf(Assert.Single(services.GetRequiredService<TraceStore>().Added));
        Assert.Equal(2, body.ModelCalls.Count);
        Assert.All(body.ModelCalls, call => Assert.Equal(
            (ModelCallKind.Extraction, Extract, false, true, "Two towels to 412, please."),
            (call.Kind, call.Task, call.ContinuesConversation, call.StructuredOutput, call.Input[0].Parts[0].Text)));
        Assert.Equal([model.ExtractionRequests[0].Instructions, model.ExtractionRequests[1].Instructions], body.ModelCalls.Select(call => call.SystemPrompt));
        Assert.Equal(TowelsJson, body.ModelCalls[1].Output!.Parts[0].Text);
        Assert.Equal(new TokenUsage(40, 8), body.Usage);
    }

    [Fact]
    public async Task Complete_CitingASourceNotRetrieved_Twice_OrRecordingAfterwards_Throws()
    {
        using ServiceProvider services = Build(new ScriptedModel(), new ExecutorScript(), _ => { });
        await using AsyncServiceScope scope = services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().Set(tenant);
        AITraceRecorder recorder = scope.ServiceProvider.GetRequiredService<AITraceRecorder>();
        AITraceCompletion completion = new(Trigger("Hi"), AITraceOutcome.Answered, "Hello.", []);

        await Assert.ThrowsAsync<InvalidOperationException>(() => recorder.CompleteAsync(completion with { Citations = [invented] }, Cancellation));
        await recorder.CompleteAsync(completion, Cancellation);

        await Assert.ThrowsAsync<InvalidOperationException>(() => recorder.CompleteAsync(completion, Cancellation));
        Assert.Throws<InvalidOperationException>(() => recorder.RecordReply(true, null));
        Assert.Single(services.GetRequiredService<TraceStore>().Added);
    }

    [Fact]
    public async Task Complete_WithNothingRecorded_StartsWhenItCompletes()
    {
        using ServiceProvider services = Build(new ScriptedModel(), new ExecutorScript(), _ => { });
        await using AsyncServiceScope scope = services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().Set(tenant);

        await scope.ServiceProvider.GetRequiredService<IAITraceRecorder>()
            .CompleteAsync(new AITraceCompletion(new AITraceTrigger("ContentItemCreated", null, null), AITraceOutcome.Failed, null, []), Cancellation);

        AITrace trace = Assert.Single(services.GetRequiredService<TraceStore>().Added);
        Assert.Equal((Now, Now, Actor.System), (trace.StartedAt, trace.CompletedAt, trace.Actor));
        Assert.Equal(new AITraceBody(null, [], [], [], null, new TraceResponse(null, []), new TokenUsage(0, 0)), BodyOf(trace));
    }

    private static async Task<AITrace> AnswerAndCompleteAsync(ServiceProvider services, string asked, string? intent = null)
    {
        await using AsyncServiceScope scope = services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().Set(tenant);

        Result<GroundedAnswer> answer = await scope.ServiceProvider.GetRequiredService<IGroundedAnswerer>()
            .AnswerAsync(new GroundedQuestion(Grounded, auroraGrand, [ChatMessage.User(asked)], NoSource, Values), Cancellation);
        Assert.True(answer.IsSuccess, answer.IsFailure ? answer.Error.Message : null);
        ((FakeTimeProvider)services.GetRequiredService<TimeProvider>()).Advance(TimeSpan.FromSeconds(2));

        AITraceId id = await scope.ServiceProvider.GetRequiredService<IAITraceRecorder>()
            .CompleteAsync(AITraceCompletion.For(Trigger(asked), auroraGrand, answer.Value, intent), Cancellation);

        AITrace trace = Assert.Single(services.GetRequiredService<TraceStore>().Added);
        Assert.Equal(id, trace.Id);

        return trace;
    }

    private static async Task<AITrace> RunLoopAsync(ServiceProvider services)
    {
        await using AsyncServiceScope scope = services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().Set(tenant);

        await scope.ServiceProvider.GetRequiredService<IToolLoop>().RunAsync(new ToolLoopRequest(Concierge, [ChatMessage.User("Book me a table.")], Values), Cancellation);
        await scope.ServiceProvider.GetRequiredService<IAITraceRecorder>()
            .CompleteAsync(new AITraceCompletion(Trigger("Book me a table."), AITraceOutcome.Answered, "Sorry.", []), Cancellation);

        return Assert.Single(services.GetRequiredService<TraceStore>().Added);
    }

    private static AITraceBody BodyOf(AITrace trace) => JsonSerializer.Deserialize<AITraceBody>(trace.Body, ContractJson.Strict)!;

    private static AITraceTrigger Trigger(string text) => new(AITraceTrigger.GuestMessage, message, text);

    private static ExecutorScript Context()
    {
        ExecutorScript executor = new();
        executor.Results.Enqueue(JsonSerializer.SerializeToElement(new { source = Source(propertySource), name = "Aurora Grand" }));
        executor.Results.Enqueue(JsonSerializer.SerializeToElement(new { results = new[] { new { source = Source(wifiSource), title = "Wi-Fi", body = "Join the Aurora-Guest network." } } }));

        return executor;
    }

    private static object Source(SourceReference source) => new { type = source.Type, id = source.Id, version = source.Version };

    private static string Reply(string status, string answer, params SourceReference[] citations) =>
        JsonSerializer.Serialize(new { status, answer, citations = citations.Select(Source) });
}
