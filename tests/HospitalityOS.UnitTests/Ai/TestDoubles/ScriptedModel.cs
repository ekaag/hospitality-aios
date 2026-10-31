using System.Collections.Concurrent;
using System.Text.Json;
using HospitalityOS.Ports.Bindings;
using HospitalityOS.Ports.Inference;
using HospitalityOS.SharedKernel.Collections;
using Microsoft.Extensions.Configuration;

namespace HospitalityOS.UnitTests.Ai.TestDoubles;

/// <summary>
/// A chat model, extractor, and classifier that answer from a queue of scripted replies, in order, and record every request. A
/// reply may throw (e.g. <see cref="InferenceUnavailableException"/>) to simulate a failure.
/// </summary>
internal sealed class ScriptedModel : IChatModel, IStructuredExtractor, IClassifier
{
    private readonly ConcurrentQueue<Func<ChatRequest, ChatCompletion>> chatReplies = new();
    private readonly ConcurrentQueue<string> extractionReplies = new();
    private readonly ConcurrentQueue<string> labels = new();

    public List<ChatRequest> ChatRequests { get; } = [];

    public List<ExtractionRequest> ExtractionRequests { get; } = [];

    public List<ClassificationRequest> ClassificationRequests { get; } = [];

    public static ChatCompletion Answer(string text, ChatStopReason stop = ChatStopReason.Completed) =>
        new(ChatMessage.Assistant(text), stop, new InferenceUsage(10, 5), "scripted-model");

    public static ChatCompletion Calls(params (string Id, string Tool, string Arguments)[] calls) =>
        new(
            new ChatMessage(ChatRole.Assistant, ValueList.From<ChatContent>([.. calls.Select(call => new ChatToolCall(call.Id, call.Tool, JsonDocument.Parse(call.Arguments).RootElement.Clone()))])),
            ChatStopReason.ToolCalls,
            new InferenceUsage(10, 5),
            "scripted-model");

    public ScriptedModel Then(ChatCompletion reply) => Then(_ => reply);

    public ScriptedModel Then(Func<ChatRequest, ChatCompletion> reply)
    {
        chatReplies.Enqueue(reply);

        return this;
    }

    public ScriptedModel ThenClassify(string label)
    {
        labels.Enqueue(label);

        return this;
    }

    public ScriptedModel ThenExtract(string json)
    {
        extractionReplies.Enqueue(json);

        return this;
    }

    public Task<ChatCompletion> CompleteAsync(ChatRequest request, ModelSelection model, CancellationToken cancellationToken)
    {
        ChatRequests.Add(request);

        return chatReplies.TryDequeue(out Func<ChatRequest, ChatCompletion>? reply)
            ? Task.FromResult(reply(request))
            : throw new InvalidOperationException("The script has no more chat replies.");
    }

    public Task<Extraction> ExtractAsync(ExtractionRequest request, ModelSelection model, CancellationToken cancellationToken)
    {
        ExtractionRequests.Add(request);

        return extractionReplies.TryDequeue(out string? json)
            ? Task.FromResult(new Extraction(JsonDocument.Parse(json).RootElement.Clone(), new InferenceUsage(20, 4), "scripted-model"))
            : throw new InvalidOperationException("The script has no more extraction replies.");
    }

    public Task<Classification> ClassifyAsync(ClassificationRequest request, ModelSelection model, CancellationToken cancellationToken)
    {
        ClassificationRequests.Add(request);

        return labels.TryDequeue(out string? label)
            ? Task.FromResult(new Classification(label, null, new InferenceUsage(15, 1), "scripted-model"))
            : throw new InvalidOperationException("The script has no more labels.");
    }
}

/// <summary>
/// Serves the one <see cref="ScriptedModel"/> under adapter name <c>Scripted</c>.
/// </summary>
internal sealed class ScriptedModelFactory<TPort>(ScriptedModel model) : IAdapterFactory<TPort>
    where TPort : class
{
    public string AdapterName => "Scripted";

    public TPort Create(IServiceProvider services, IConfiguration settings) => (TPort)(object)model;
}
