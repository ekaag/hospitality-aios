using System.ClientModel;
using System.Net;
using System.Text.Json;
using HospitalityOS.Adapters.AzureOpenAI;
using HospitalityOS.Ports.Inference;
using Xunit;

namespace HospitalityOS.UnitTests.Adapters.AzureOpenAI;

/// <summary>
/// The Azure OpenAI adapter's mapping to and from Chat Completions and embeddings, against recorded responses.
/// </summary>
public sealed class AzureOpenAIModelTests : IDisposable
{
    private static readonly ChatTool localTime = new(
        "get_local_time",
        "Returns the hotel's local time.",
        JsonDocument.Parse("""{"type":"object","properties":{"zone":{"type":"string"}},"required":["zone"]}""").RootElement.Clone());

    private static readonly ModelSelection mini = new("gpt-5-mini", InferenceParameters.Default);

    private readonly RecordedAzureOpenAIApi api = new();

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    public void Dispose() => api.Dispose();

    [Fact]
    public async Task Complete_SendsTheDeploymentEffortLengthAndTools_ToTheResourcesV1Api()
    {
        api.Returns(RecordedAzureOpenAIApi.Completion("stop", RecordedAzureOpenAIApi.Text("The pool closes at 22:00.")));

        await api.Model().CompleteAsync(
            new ChatRequest("You are a concierge.", [ChatMessage.User("When does the pool close?")], [localTime]),
            new ModelSelection("gpt-5-mini", new InferenceParameters(800, InferenceEffort.Low)),
            Cancellation);

        Assert.Equal(new Uri(RecordedAzureOpenAIApi.Endpoint + "openai/v1/chat/completions"), api.Requests[^1].Request.RequestUri);
        Assert.Equal("Bearer test-token", api.Requests[^1].Request.Headers.Authorization?.ToString());
        JsonElement body = api.LastBody;
        Assert.Equal(("gpt-5-mini", 800, "low"), (body.GetProperty("model").GetString(), body.GetProperty("max_completion_tokens").GetInt32(), body.GetProperty("reasoning_effort").GetString()));
        Assert.Equal(
            [("system", "You are a concierge."), ("user", "When does the pool close?")],
            body.GetProperty("messages").EnumerateArray().Select(message => (message.GetProperty("role").GetString(), Content(message))));
        JsonElement tool = Assert.Single(body.GetProperty("tools").EnumerateArray());
        Assert.Equal(("function", "get_local_time", "zone"), (tool.GetProperty("type").GetString(), tool.GetProperty("function").GetProperty("name").GetString(), tool.GetProperty("function").GetProperty("parameters").GetProperty("required")[0].GetString()));
        Assert.False(body.TryGetProperty("temperature", out _));
        Assert.False(body.TryGetProperty("response_format", out _));
    }

    [Fact]
    public async Task Complete_WithDefaultParameters_LeavesLengthAndEffortToTheModel_AndMaxEffortIsHigh()
    {
        api.Returns(RecordedAzureOpenAIApi.Completion("stop", RecordedAzureOpenAIApi.Text("Hello.")))
            .Returns(RecordedAzureOpenAIApi.Completion("stop", RecordedAzureOpenAIApi.Text("Hello.")));

        await api.Model().CompleteAsync(new ChatRequest(null, [ChatMessage.User("Hi")]), mini, Cancellation);
        Assert.False(api.LastBody.TryGetProperty("max_completion_tokens", out _));
        Assert.False(api.LastBody.TryGetProperty("reasoning_effort", out _));
        Assert.False(api.LastBody.TryGetProperty("tools", out _));

        await api.Model().CompleteAsync(new ChatRequest(null, [ChatMessage.User("Hi")]), new ModelSelection("gpt-5-mini", new InferenceParameters(Effort: InferenceEffort.Max)), Cancellation);
        Assert.Equal("high", api.LastBody.GetProperty("reasoning_effort").GetString());
    }

    [Fact]
    public async Task Complete_MapsTextAndToolCalls_AndReportsUsageAndTheModelThatAnswered()
    {
        api.Returns(RecordedAzureOpenAIApi.Completion(
            "tool_calls",
            """
            "content":"Let me check.",
            "tool_calls":[{"id":"call_01","type":"function","function":{"name":"get_local_time","arguments":"{\"zone\":\"Europe/London\"}"}}]
            """));

        ChatCompletion completion = await api.Model().CompleteAsync(new ChatRequest(null, [ChatMessage.User("What time is it?")], [localTime]), mini, Cancellation);

        Assert.Equal(ChatStopReason.ToolCalls, completion.StopReason);
        Assert.Equal(("Let me check.", "gpt-5-mini-2025-08-07", new InferenceUsage(120, 45)), (completion.Message.Text, completion.Model, completion.Usage));
        ChatToolCall call = Assert.Single(completion.Message.Content.OfType<ChatToolCall>());
        Assert.Equal(("call_01", "get_local_time", "Europe/London"), (call.CallId, call.Name, call.Arguments.GetProperty("zone").GetString()));
    }

    [Fact]
    public async Task Complete_AfterToolCalls_SendsTheCallsAndEachResultAsAToolMessage_AndDropsReasoning()
    {
        api.Returns(RecordedAzureOpenAIApi.Completion("stop", RecordedAzureOpenAIApi.Text("It is 21:00.")));
        JsonElement arguments = JsonDocument.Parse("""{"zone":"Europe/London"}""").RootElement.Clone();

        // A conversation Claude began, continued here after a fallback: its reasoning means nothing to this provider.
        await api.Model().CompleteAsync(
            new ChatRequest(null, [
                ChatMessage.User("What time is it, and is the bar open?"),
                new ChatMessage(ChatRole.Assistant, [new ChatReasoning("Anthropic", "{}"), new ChatText("Checking."), new ChatToolCall("toolu_01", "get_local_time", arguments), new ChatToolCall("toolu_02", "get_local_time", arguments)]),
                new ChatMessage(ChatRole.Tool, [new ChatToolResult("toolu_01", "21:00"), new ChatToolResult("toolu_02", "unknown zone", IsError: true)]),
                ChatMessage.User("Thanks"),
            ], [localTime]),
            mini,
            Cancellation);

        JsonElement[] messages = [.. api.LastBody.GetProperty("messages").EnumerateArray()];
        Assert.Equal(["user", "assistant", "tool", "tool", "user"], messages.Select(message => message.GetProperty("role").GetString()));
        Assert.Equal("Checking.", Content(messages[1]));
        Assert.Equal(
            [("toolu_01", "get_local_time", "Europe/London"), ("toolu_02", "get_local_time", "Europe/London")],
            messages[1].GetProperty("tool_calls").EnumerateArray().Select(call => (
                call.GetProperty("id").GetString(),
                call.GetProperty("function").GetProperty("name").GetString(),
                JsonDocument.Parse(call.GetProperty("function").GetProperty("arguments").GetString()!).RootElement.GetProperty("zone").GetString())));
        Assert.Equal(("toolu_01", "21:00"), (messages[2].GetProperty("tool_call_id").GetString(), Content(messages[2])));
        Assert.Equal(("toolu_02", "Error: unknown zone"), (messages[3].GetProperty("tool_call_id").GetString(), Content(messages[3])));
    }

    [Theory]
    [InlineData("length", ChatStopReason.MaxTokens)]
    [InlineData("content_filter", ChatStopReason.ContentFiltered)]
    [InlineData("stop", ChatStopReason.Completed)]
    public async Task Complete_MapsWhyTheModelStopped(string finishReason, ChatStopReason expected)
    {
        api.Returns(RecordedAzureOpenAIApi.Completion(finishReason, RecordedAzureOpenAIApi.Text("…")));

        ChatCompletion completion = await api.Model().CompleteAsync(new ChatRequest(null, [ChatMessage.User("Hi")]), mini, Cancellation);

        Assert.Equal(expected, completion.StopReason);
    }

    [Fact]
    public async Task Complete_WithAResponseSchema_AsksForIt_StrictlyWhenTheSchemaAllowsIt()
    {
        api.Returns(RecordedAzureOpenAIApi.Completion("stop", RecordedAzureOpenAIApi.Text("""{"answer":"22:00"}""")))
            .Returns(RecordedAzureOpenAIApi.Completion("stop", RecordedAzureOpenAIApi.Text("""{"answer":"22:00"}""")));
        JsonElement closed = JsonDocument.Parse("""{"type":"object","properties":{"answer":{"type":"string"}},"required":["answer"],"additionalProperties":false}""").RootElement.Clone();
        JsonElement open = JsonDocument.Parse("""{"type":"object","properties":{"answer":{"type":"string"},"note":{"type":"string"}},"required":["answer"]}""").RootElement.Clone();

        await api.Model().CompleteAsync(new ChatRequest(null, [ChatMessage.User("When does the pool close?")], [localTime], closed), mini, Cancellation);
        JsonElement format = api.LastBody.GetProperty("response_format");
        Assert.Equal(("json_schema", true), (format.GetProperty("type").GetString(), format.GetProperty("json_schema").GetProperty("strict").GetBoolean()));
        Assert.Equal("answer", format.GetProperty("json_schema").GetProperty("schema").GetProperty("required")[0].GetString());

        await api.Model().CompleteAsync(new ChatRequest(null, [ChatMessage.User("When does the pool close?")], null, open), mini, Cancellation);
        Assert.False(api.LastBody.GetProperty("response_format").GetProperty("json_schema").GetProperty("strict").GetBoolean());
    }

    [Theory]
    [InlineData("""{"type":"object","properties":{"a":{"type":"string"}},"required":["a"],"additionalProperties":false}""", true)]
    [InlineData("""{"type":"object","properties":{"a":{"type":"string"},"b":{"type":"string"}},"required":["a"],"additionalProperties":false}""", false)]
    [InlineData("""{"type":"object","properties":{"a":{"type":"string"}},"required":["a"]}""", false)]
    [InlineData("""{"type":"object","properties":{"a":{"type":"object","properties":{"b":{"type":"string"}},"required":["b"]}},"required":["a"],"additionalProperties":false}""", false)]
    [InlineData("""{"type":"object","properties":{"a":{"type":"array","items":{"type":"object","properties":{"b":{"type":"string"}},"required":["b"],"additionalProperties":false}}},"required":["a"],"additionalProperties":false}""", true)]
    public void IsStrict_OnlyWhenEveryObjectIsClosedAndRequiresAllItsProperties(string schema, bool strict)
    {
        Assert.Equal(strict, AzureOpenAIMessages.IsStrict(JsonDocument.Parse(schema).RootElement));
    }

    [Fact]
    public async Task Extract_AsksForTheNamedSchema_AndReturnsTheJsonValue()
    {
        api.Returns(RecordedAzureOpenAIApi.Completion("stop", RecordedAzureOpenAIApi.Text("""{"item":"bath_towel","quantity":2}""")));
        JsonElement schema = JsonDocument.Parse("""{"type":"object","properties":{"item":{"type":"string"},"quantity":{"type":"integer"}},"required":["item","quantity"],"additionalProperties":false}""").RootElement.Clone();

        Extraction extraction = await api.Model().ExtractAsync(new ExtractionRequest("Extract the request.", "Two towels please", "request", schema), mini, Cancellation);

        Assert.Equal(("bath_towel", 2), (extraction.Output.GetProperty("item").GetString(), extraction.Output.GetProperty("quantity").GetInt32()));
        JsonElement body = api.LastBody;
        Assert.Equal(("request", true), (body.GetProperty("response_format").GetProperty("json_schema").GetProperty("name").GetString(), body.GetProperty("response_format").GetProperty("json_schema").GetProperty("strict").GetBoolean()));
        Assert.Equal(
            [("system", "Extract the request."), ("user", "Two towels please")],
            body.GetProperty("messages").EnumerateArray().Select(message => (message.GetProperty("role").GetString(), Content(message))));
    }

    [Fact]
    public async Task Classify_ConstrainsTheLabelToThoseGiven_AndClampsTheConfidence()
    {
        api.Returns(RecordedAzureOpenAIApi.Completion("stop", RecordedAzureOpenAIApi.Text("""{"label":"request","confidence":1.4}""")));

        Classification classification = await api.Model().ClassifyAsync(new ClassificationRequest("Classify the message.", "Two towels please", ["question", "request"]), mini, Cancellation);

        Assert.Equal(("request", 1.0), (classification.Label, classification.Confidence));
        JsonElement labels = api.LastBody.GetProperty("response_format").GetProperty("json_schema").GetProperty("schema").GetProperty("properties").GetProperty("label").GetProperty("enum");
        Assert.Equal(["question", "request"], labels.EnumerateArray().Select(label => label.GetString()));
    }

    [Fact]
    public async Task Judge_ReturnsTheVerdict()
    {
        api.Returns(RecordedAzureOpenAIApi.Completion("stop", RecordedAzureOpenAIApi.Text("""{"reasoning":"Cites the hours.","score":0.9,"passed":true}""")));

        Judgement judgement = await api.Model().JudgeAsync(new JudgeRequest("Answers with the hours.", "When does the pool close?", "At 22:00."), mini, Cancellation);

        Assert.Equal((0.9, true, "Cites the hours."), (judgement.Score, judgement.Passed, judgement.Reasoning));
    }

    [Theory]
    [InlineData("length", "\"content\":\"{\\\"label\\\":\"")]
    [InlineData("content_filter", "\"content\":null")]
    [InlineData("stop", "\"content\":null,\"refusal\":\"I can't help with that.\"")]
    public async Task StructuredTasks_ThatAreCutOffFilteredOrRefused_AreUnavailable_SoAnotherProviderCanAnswer(string finishReason, string message)
    {
        api.Returns(RecordedAzureOpenAIApi.Completion(finishReason, message));

        await Assert.ThrowsAsync<InferenceUnavailableException>(() =>
            api.Model().ClassifyAsync(new ClassificationRequest("Classify.", "Hi", ["question", "request"]), mini, Cancellation));
    }

    [Fact]
    public async Task Embed_SendsTheInputsAndDimensions_AndReturnsVectorsInInputOrder()
    {
        api.Returns("""
            {"object":"list","model":"text-embedding-3-small","data":[
              {"object":"embedding","index":1,"embedding":[0.0,1.0]},
              {"object":"embedding","index":0,"embedding":[1.0,0.0]}],
             "usage":{"prompt_tokens":7,"total_tokens":7}}
            """);

        Embeddings embeddings = await api.Model().EmbedAsync(new EmbeddingRequest(["pool hours", "spa hours"], 2), new ModelSelection("text-embedding-3-small", InferenceParameters.Default), Cancellation);

        Assert.Equal(new Uri(RecordedAzureOpenAIApi.Endpoint + "openai/v1/embeddings"), api.Requests[^1].Request.RequestUri);
        Assert.Equal(("text-embedding-3-small", 2), (api.LastBody.GetProperty("model").GetString(), api.LastBody.GetProperty("dimensions").GetInt32()));
        Assert.Equal(["pool hours", "spa hours"], api.LastBody.GetProperty("input").EnumerateArray().Select(input => input.GetString()));
        Assert.Equal([[1f, 0f], [0f, 1f]], embeddings.Vectors.Select(vector => vector.ToArray()));
        Assert.Equal((new InferenceUsage(7, 0), "text-embedding-3-small"), (embeddings.Usage, embeddings.Model));
    }

    [Theory]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.RequestTimeout)]
    public async Task Complete_WhenRateLimitedOrFailing_IsUnavailable(HttpStatusCode status)
    {
        api.Returns(RecordedAzureOpenAIApi.Error("server_error", "Try again later."), status);

        await Assert.ThrowsAsync<InferenceUnavailableException>(() => api.Model().CompleteAsync(new ChatRequest(null, [ChatMessage.User("Hi")]), mini, Cancellation));
    }

    [Fact]
    public async Task Complete_APromptTheContentFilterDeclines_IsUnavailable_SoAnotherProviderCanAnswer()
    {
        api.Returns(RecordedAzureOpenAIApi.Error("content_filter", "The prompt was filtered."), HttpStatusCode.BadRequest);

        await Assert.ThrowsAsync<InferenceUnavailableException>(() => api.Model().CompleteAsync(new ChatRequest(null, [ChatMessage.User("Hi")]), mini, Cancellation));
    }

    [Fact]
    public async Task Complete_WhenTheResourceCannotBeReached_OrTimesOut_IsUnavailable()
    {
        api.Fails(new HttpRequestException("No such host."));
        await Assert.ThrowsAsync<InferenceUnavailableException>(() => api.Model().CompleteAsync(new ChatRequest(null, [ChatMessage.User("Hi")]), mini, Cancellation));

        api.Hangs();
        await Assert.ThrowsAsync<InferenceUnavailableException>(() =>
            api.Model(TimeSpan.FromMilliseconds(200)).CompleteAsync(new ChatRequest(null, [ChatMessage.User("Hi")]), mini, Cancellation));
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.Unauthorized)]
    public async Task Complete_ARejectedRequest_IsNotUnavailable_SoNoFallbackHidesIt(HttpStatusCode status)
    {
        api.Returns(RecordedAzureOpenAIApi.Error("DeploymentNotFound", "The deployment does not exist."), status);

        ClientResultException exception = await Assert.ThrowsAsync<ClientResultException>(() =>
            api.Model().CompleteAsync(new ChatRequest(null, [ChatMessage.User("Hi")]), mini, Cancellation));

        Assert.Equal((int)status, exception.Status);
    }

    [Fact]
    public async Task Complete_CancelledByTheCaller_IsCancelled()
    {
        api.Hangs();
        using CancellationTokenSource cancellation = CancellationTokenSource.CreateLinkedTokenSource(Cancellation);
        cancellation.CancelAfter(TimeSpan.FromMilliseconds(100));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => api.Model().CompleteAsync(new ChatRequest(null, [ChatMessage.User("Hi")]), mini, cancellation.Token));
    }

    [Fact]
    public async Task CheckHealth_ListsTheResourcesModels()
    {
        api.Returns("""{"object":"list","data":[]}""").Returns(RecordedAzureOpenAIApi.Error("PermissionDenied", "No role."), HttpStatusCode.Forbidden);

        Assert.True((await api.Model().CheckHealthAsync(Cancellation)).IsHealthy);
        Assert.Equal(new Uri(RecordedAzureOpenAIApi.Endpoint + "openai/v1/models"), api.Requests[^1].Request.RequestUri);
        Assert.False((await api.Model().CheckHealthAsync(Cancellation)).IsHealthy);
    }

    private static string? Content(JsonElement message) =>
        message.GetProperty("content") switch
        {
            { ValueKind: JsonValueKind.String } text => text.GetString(),
            { ValueKind: JsonValueKind.Array } parts => string.Concat(parts.EnumerateArray().Select(part => part.GetProperty("text").GetString())),
            _ => null,
        };
}
