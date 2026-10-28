using System.Net;
using System.Text.Json;
using Anthropic.Exceptions;
using HospitalityOS.Adapters.Anthropic;
using HospitalityOS.Ports.Inference;
using Xunit;

namespace HospitalityOS.UnitTests.Adapters.Anthropic;

/// <summary>
/// The Anthropic adapter's mapping to and from the Messages API, against recorded responses.
/// </summary>
public sealed class AnthropicModelTests : IDisposable
{
    private static readonly ChatTool localTime = new(
        "get_local_time",
        "Returns the hotel's local time.",
        JsonDocument.Parse("""{"type":"object","properties":{"zone":{"type":"string"}},"required":["zone"]}""").RootElement.Clone());

    private readonly RecordedAnthropicApi api = new();

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    public void Dispose() => api.Dispose();

    [Fact]
    public async Task Complete_SendsTheTaskModelEffortToolsAndRefusalFallback_AndNoSamplingSettings()
    {
        api.Returns(RecordedAnthropicApi.Message("end_turn", """{"type":"text","text":"The pool closes at 22:00."}"""));

        await api.Model().CompleteAsync(
            new ChatRequest("You are a concierge.", [ChatMessage.User("When does the pool close?")], [localTime]),
            new ModelSelection("claude-sonnet-5-5", new InferenceParameters(800, InferenceEffort.Low)),
            Cancellation);

        JsonElement body = api.LastBody;
        Assert.Equal(("claude-sonnet-5-5", 800, "You are a concierge.", "low"), (body.GetProperty("model").GetString(), body.GetProperty("max_tokens").GetInt32(), body.GetProperty("system").GetString(), body.GetProperty("output_config").GetProperty("effort").GetString()));
        Assert.Equal("default", body.GetProperty("fallbacks").GetString());
        Assert.Contains(AnthropicModel.FallbackBeta, api.Requests[^1].Request.Headers.GetValues("anthropic-beta").Single(), StringComparison.Ordinal);
        JsonElement tool = Assert.Single(body.GetProperty("tools").EnumerateArray());
        Assert.Equal(("get_local_time", "object", "zone"), (tool.GetProperty("name").GetString(), tool.GetProperty("input_schema").GetProperty("type").GetString(), tool.GetProperty("input_schema").GetProperty("required")[0].GetString()));
        Assert.False(body.TryGetProperty("temperature", out _));
        Assert.False(body.TryGetProperty("top_p", out _));
    }

    [Theory]
    [InlineData("claude-haiku-5-5", true)]
    [InlineData("claude-sonnet-5-5", false)]
    public async Task Complete_OnHaikuOrWithFallbackOff_SendsNoRefusalFallback(string model, bool refusalFallback)
    {
        api.Returns(RecordedAnthropicApi.Message("end_turn", """{"type":"text","text":"Hello."}""", model));

        await api.Model(refusalFallback).CompleteAsync(new ChatRequest(null, [ChatMessage.User("Hi")]), new ModelSelection(model, InferenceParameters.Default), Cancellation);

        Assert.False(api.LastBody.TryGetProperty("fallbacks", out _));
        string betas = api.Requests[^1].Request.Headers.TryGetValues("anthropic-beta", out IEnumerable<string>? values) ? string.Join(",", values) : "";
        Assert.DoesNotContain(AnthropicModel.FallbackBeta, betas, StringComparison.Ordinal);
        Assert.Equal(16000, api.LastBody.GetProperty("max_tokens").GetInt32());
        Assert.False(api.LastBody.TryGetProperty("system", out _));
    }

    [Fact]
    public async Task Complete_MapsThinkingTextAndToolCalls_AndReportsUsageAndTheModelThatAnswered()
    {
        api.Returns(RecordedAnthropicApi.Message(
            "tool_use",
            """
            {"type":"thinking","thinking":"","signature":"sig-abc"},
            {"type":"text","text":"Let me check."},
            {"type":"tool_use","id":"toolu_01","name":"get_local_time","input":{"zone":"Europe/London"}}
            """,
            "claude-opus-5"));

        ChatCompletion completion = await api.Model().CompleteAsync(
            new ChatRequest(null, [ChatMessage.User("What time is it?")], [localTime]), new ModelSelection("claude-sonnet-5-5", InferenceParameters.Default), Cancellation);

        Assert.Equal(ChatStopReason.ToolCalls, completion.StopReason);
        Assert.Equal(("Let me check.", "claude-opus-5", new InferenceUsage(160, 45)), (completion.Message.Text, completion.Model, completion.Usage));
        ChatReasoning reasoning = Assert.IsType<ChatReasoning>(completion.Message.Content[0]);
        Assert.Equal(AnthropicModel.ProviderName, reasoning.Provider);
        ChatToolCall call = Assert.IsType<ChatToolCall>(completion.Message.Content[2]);
        Assert.Equal(("toolu_01", "get_local_time", "Europe/London"), (call.CallId, call.Name, call.Arguments.GetProperty("zone").GetString()));
    }

    [Fact]
    public async Task Complete_AfterAToolCall_SendsTheThinkingBackUnchanged_ToolResultsInAUserTurn_AndDropsOtherProvidersReasoning()
    {
        api.Returns(RecordedAnthropicApi.Message(
                "tool_use",
                """
                {"type":"thinking","thinking":"","signature":"sig-abc"},
                {"type":"redacted_thinking","data":"opaque-xyz"},
                {"type":"tool_use","id":"toolu_01","name":"get_local_time","input":{"zone":"Europe/London"}}
                """))
            .Returns(RecordedAnthropicApi.Message("end_turn", """{"type":"text","text":"It is 21:40."}"""));
        AnthropicModel model = api.Model();
        ModelSelection sonnet = new("claude-sonnet-5-5", InferenceParameters.Default);
        ChatMessage question = ChatMessage.User("What time is it?");
        ChatCompletion asked = await model.CompleteAsync(new ChatRequest(null, [question], [localTime]), sonnet, Cancellation);

        ChatCompletion answered = await model.CompleteAsync(
            new ChatRequest(
                null,
                [
                    question,
                    asked.Message,
                    new ChatMessage(ChatRole.Tool, [new ChatToolResult("toolu_01", """{"localTime":"21:40"}""")]),
                    new ChatMessage(ChatRole.Assistant, [new ChatReasoning("OtherProvider", "state"), new ChatText("(earlier answer)")]),
                    ChatMessage.User("Thanks"),
                ],
                [localTime]),
            sonnet,
            Cancellation);

        Assert.Equal("It is 21:40.", answered.Message.Text);
        JsonElement[] turns = [.. api.LastBody.GetProperty("messages").EnumerateArray()];
        Assert.Equal(["user", "assistant", "user", "assistant", "user"], turns.Select(turn => turn.GetProperty("role").GetString()));
        JsonElement[] replayed = [.. turns[1].GetProperty("content").EnumerateArray()];
        Assert.Equal(("thinking", "sig-abc"), (replayed[0].GetProperty("type").GetString(), replayed[0].GetProperty("signature").GetString()));
        Assert.Equal(("redacted_thinking", "opaque-xyz"), (replayed[1].GetProperty("type").GetString(), replayed[1].GetProperty("data").GetString()));
        Assert.Equal(("tool_use", "toolu_01"), (replayed[2].GetProperty("type").GetString(), replayed[2].GetProperty("id").GetString()));
        JsonElement result = turns[2].GetProperty("content")[0];
        Assert.Equal(("tool_result", "toolu_01"), (result.GetProperty("type").GetString(), result.GetProperty("tool_use_id").GetString()));
        Assert.Equal(["text"], turns[3].GetProperty("content").EnumerateArray().Select(block => block.GetProperty("type").GetString()));
    }

    [Fact]
    public async Task Complete_AGuestMessageRightAfterToolResults_JoinsThemInOneUserTurn_AsTheApiRequiresAlternation()
    {
        api.Returns(RecordedAnthropicApi.Message("end_turn", """{"type":"text","text":"The gym opens at 06:00."}"""));
        ChatToolCall call = new("toolu_01", "get_local_time", JsonDocument.Parse("""{"zone":"Europe/London"}""").RootElement.Clone());

        await api.Model().CompleteAsync(
            new ChatRequest(
                null,
                [
                    ChatMessage.User("What time is it?"),
                    new ChatMessage(ChatRole.Assistant, [call]),
                    new ChatMessage(ChatRole.Tool, [new ChatToolResult("toolu_01", """{"localTime":"21:40"}""")]),
                    ChatMessage.User("And when does the gym open?"),
                ],
                [localTime]),
            new ModelSelection("claude-sonnet-5-5", InferenceParameters.Default),
            Cancellation);

        JsonElement[] turns = [.. api.LastBody.GetProperty("messages").EnumerateArray()];
        Assert.Equal(["user", "assistant", "user"], turns.Select(turn => turn.GetProperty("role").GetString()));
        Assert.Equal(["tool_result", "text"], turns[2].GetProperty("content").EnumerateArray().Select(block => block.GetProperty("type").GetString()));
    }

    [Fact]
    public async Task Complete_ARefusal_IsContentFiltered()
    {
        api.Returns(RecordedAnthropicApi.Message("refusal", ""));

        ChatCompletion completion = await api.Model().CompleteAsync(
            new ChatRequest(null, [ChatMessage.User("...")]), new ModelSelection("claude-sonnet-5-5", InferenceParameters.Default), Cancellation);

        Assert.Equal((ChatStopReason.ContentFiltered, ""), (completion.StopReason, completion.Message.Text));
    }

    [Fact]
    public async Task Complete_WithAResponseSchema_AsksForItAlongsideTheTools_AndWithoutOneAsksForNoFormat()
    {
        api.Returns(RecordedAnthropicApi.Message("end_turn", """{"type":"text","text":"{\"status\":\"Unknown\"}"}"""));
        api.Returns(RecordedAnthropicApi.Message("end_turn", """{"type":"text","text":"Hello."}"""));
        JsonElement schema = JsonDocument.Parse("""{"type":"object","properties":{"status":{"type":"string"}},"required":["status"],"additionalProperties":false}""").RootElement.Clone();
        ModelSelection sonnet = new("claude-sonnet-5-5", new InferenceParameters(null, InferenceEffort.Low));

        await api.Model().CompleteAsync(new ChatRequest("Reply as JSON.", [ChatMessage.User("Helicopters?")], [localTime], schema), sonnet, Cancellation);
        JsonElement structured = api.LastBody;
        await api.Model().CompleteAsync(new ChatRequest("Reply.", [ChatMessage.User("Hi")], [localTime]), sonnet, Cancellation);

        JsonElement format = structured.GetProperty("output_config").GetProperty("format");
        Assert.Equal(("json_schema", "string", 1), (format.GetProperty("type").GetString(), format.GetProperty("schema").GetProperty("properties").GetProperty("status").GetProperty("type").GetString(), structured.GetProperty("tools").GetArrayLength()));
        Assert.False(api.LastBody.GetProperty("output_config").TryGetProperty("format", out _));
    }

    [Fact]
    public async Task Extract_AsksForTheSchema_AndReturnsTheJsonValue()
    {
        api.Returns(RecordedAnthropicApi.Message("end_turn", """{"type":"text","text":"{\"item\":\"bath_towel\",\"quantity\":2}"}""", "claude-haiku-5-5"));
        JsonElement schema = JsonDocument.Parse("""{"type":"object","properties":{"item":{"type":"string"},"quantity":{"type":"integer"}},"required":["item","quantity"],"additionalProperties":false}""").RootElement.Clone();

        Extraction extraction = await api.Model().ExtractAsync(
            new ExtractionRequest("Extract the request.", "Two towels please", "towels", schema), new ModelSelection("claude-haiku-5-5", InferenceParameters.Default), Cancellation);

        Assert.Equal(2, extraction.Output.GetProperty("quantity").GetInt32());
        JsonElement format = api.LastBody.GetProperty("output_config").GetProperty("format");
        Assert.Equal(("json_schema", "integer"), (format.GetProperty("type").GetString(), format.GetProperty("schema").GetProperty("properties").GetProperty("quantity").GetProperty("type").GetString()));
        Assert.False(api.LastBody.GetProperty("output_config").TryGetProperty("effort", out _));
        Assert.Equal(("Extract the request.", 4096), (api.LastBody.GetProperty("system").GetString(), api.LastBody.GetProperty("max_tokens").GetInt32()));
    }

    [Fact]
    public async Task Classify_ConstrainsTheLabelToThoseGiven_AndClampsTheConfidence()
    {
        api.Returns(RecordedAnthropicApi.Message("end_turn", """{"type":"text","text":"{\"label\":\"service_request\",\"confidence\":1.4}"}""", "claude-haiku-5-5"));

        Classification classification = await api.Model().ClassifyAsync(
            new ClassificationRequest("Route it.", "Towels please", ["question", "service_request"]), new ModelSelection("claude-haiku-5-5", InferenceParameters.Default), Cancellation);

        Assert.Equal(("service_request", 1.0), (classification.Label, classification.Confidence));
        JsonElement label = api.LastBody.GetProperty("output_config").GetProperty("format").GetProperty("schema").GetProperty("properties").GetProperty("label");
        Assert.Equal(["question", "service_request"], label.GetProperty("enum").EnumerateArray().Select(value => value.GetString()));
    }

    [Fact]
    public async Task Judge_ReturnsTheVerdict()
    {
        api.Returns(RecordedAnthropicApi.Message("end_turn", """{"type":"text","text":"{\"reasoning\":\"States 22:00.\",\"score\":0.9,\"passed\":true}"}""", "claude-opus-5-5"));

        Judgement judgement = await api.Model().JudgeAsync(
            new JudgeRequest("States the closing time.", "When does the pool close?", "At 22:00.", "22:00"), new ModelSelection("claude-opus-5-5", InferenceParameters.Default), Cancellation);

        Assert.Equal((0.9, true, "States 22:00.", "claude-opus-5-5"), (judgement.Score, judgement.Passed, judgement.Reasoning, judgement.Model));
    }

    [Theory]
    [InlineData("refusal")]
    [InlineData("max_tokens")]
    public async Task StructuredTasks_ThatAreDeclinedOrCutOff_AreUnavailable_SoAnotherProviderCanAnswer(string stopReason)
    {
        api.Returns(RecordedAnthropicApi.Message(stopReason, """{"type":"text","text":"{\"label\":"}""", "claude-haiku-5-5"));

        await Assert.ThrowsAsync<InferenceUnavailableException>(() => api.Model().ClassifyAsync(
            new ClassificationRequest("Route it.", "...", ["question", "service_request"]), new ModelSelection("claude-haiku-5-5", InferenceParameters.Default), Cancellation));
    }

    [Theory]
    [InlineData(HttpStatusCode.TooManyRequests, "rate_limit_error")]
    [InlineData((HttpStatusCode)529, "overloaded_error")]
    [InlineData(HttpStatusCode.InternalServerError, "api_error")]
    [InlineData(HttpStatusCode.ServiceUnavailable, "api_error")]
    public async Task Complete_WhenClaudeIsRateLimitedOverloadedOrFailing_IsUnavailable(HttpStatusCode status, string type)
    {
        api.Returns(RecordedAnthropicApi.Error(type, "Try again later."), status);

        await Assert.ThrowsAsync<InferenceUnavailableException>(() => Chat(api.Model()));
    }

    [Fact]
    public async Task Complete_WhenClaudeCannotBeReached_OrTimesOut_IsUnavailable()
    {
        api.Fails(new HttpRequestException("Connection refused")).Hangs();

        await Assert.ThrowsAsync<InferenceUnavailableException>(() => Chat(api.Model()));
        await Assert.ThrowsAsync<InferenceUnavailableException>(() => Chat(api.Model(timeout: TimeSpan.FromMilliseconds(200))));
    }

    [Fact]
    public async Task Complete_ARejectedRequest_IsNotUnavailable_SoNoFallbackHidesIt()
    {
        api.Returns(RecordedAnthropicApi.Error("invalid_request_error", "messages: roles must alternate"), HttpStatusCode.BadRequest);

        Exception exception = await Assert.ThrowsAnyAsync<Exception>(() => Chat(api.Model()));

        Assert.IsAssignableFrom<AnthropicApiException>(exception);
    }

    [Fact]
    public async Task Complete_CancelledByTheCaller_IsCancelled()
    {
        api.Hangs();
        using CancellationTokenSource cancellation = new(TimeSpan.FromMilliseconds(100));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => api.Model().CompleteAsync(
            new ChatRequest(null, [ChatMessage.User("Hi")]), new ModelSelection("claude-sonnet-5-5", InferenceParameters.Default), cancellation.Token));
    }

    private static Task<ChatCompletion> Chat(AnthropicModel model) =>
        model.CompleteAsync(new ChatRequest(null, [ChatMessage.User("Hi")]), new ModelSelection("claude-sonnet-5-5", InferenceParameters.Default), Cancellation);
}
