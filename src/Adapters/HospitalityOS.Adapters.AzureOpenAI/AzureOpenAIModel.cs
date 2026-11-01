using System.ClientModel;
using System.Text.Json;
using HospitalityOS.Ports.Bindings;
using HospitalityOS.Ports.Inference;
using HospitalityOS.SharedKernel.Collections;
using OpenAI;
using OpenAI.Chat;
using OpenAI.Embeddings;
using PortChatCompletion = HospitalityOS.Ports.Inference.ChatCompletion;
using PortChatTool = HospitalityOS.Ports.Inference.ChatTool;
using SdkChatCompletion = OpenAI.Chat.ChatCompletion;
using SdkChatMessage = OpenAI.Chat.ChatMessage;

namespace HospitalityOS.Adapters.AzureOpenAI;

/// <summary>
/// Models deployed in an Azure OpenAI (Foundry) resource, through its OpenAI v1 API: chat with tool calls, extraction,
/// classification, and judging through Chat Completions with structured outputs, and embeddings. A task's model is the
/// deployment's name. Effort maps to reasoning effort (Max to High, the most Chat Completions offers). Rate limits,
/// server errors, network failures, timeouts that outlast the SDK's retries, and prompts Azure's content filter declines
/// become <see cref="InferenceUnavailableException"/>, so the router can fall back; other rejected requests propagate.
/// Stateless apart from the client, so one instance serves concurrent calls.
/// </summary>
internal sealed class AzureOpenAIModel(OpenAIClient client)
    : IChatModel, IStructuredExtractor, IClassifier, IEvaluationJudge, IEmbeddingModel, IAdapterHealthProbe
{
    public const string ProviderName = "AzureOpenAI";

    private static readonly string[] classificationFields = ["label", "confidence"];
    private static readonly string[] judgementFields = ["reasoning", "score", "passed"];

    public async Task<PortChatCompletion> CompleteAsync(ChatRequest request, ModelSelection model, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(model);

        SdkChatCompletion completion = await SendAsync(
            model, AzureOpenAIMessages.From(request.SystemPrompt, request.Messages), Options(model, request.Tools, request.ResponseSchema), cancellationToken).ConfigureAwait(false);

        return new PortChatCompletion(AzureOpenAIMessages.ToChatMessage(completion), StopReason(completion), Usage(completion), Served(completion, model));
    }

    public async Task<Extraction> ExtractAsync(ExtractionRequest request, ModelSelection model, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(model);

        StructuredAnswer answer = await StructuredAsync(request.Instructions, request.Input, request.SchemaName, request.OutputSchema, model, cancellationToken).ConfigureAwait(false);

        return new Extraction(answer.Output, answer.Usage, answer.Model);
    }

    public async Task<Classification> ClassifyAsync(ClassificationRequest request, ModelSelection model, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(model);

        JsonElement schema = JsonSerializer.SerializeToElement(new
        {
            type = "object",
            properties = new
            {
                label = new { type = "string", @enum = request.Labels },
                confidence = new { type = "number", description = "How sure you are, from 0 to 1." },
            },
            required = classificationFields,
            additionalProperties = false,
        });
        string instructions = request.Instructions + Environment.NewLine + "Answer with exactly one of the labels.";
        StructuredAnswer answer = await StructuredAsync(instructions, request.Input, "classification", schema, model, cancellationToken).ConfigureAwait(false);

        return new Classification(
            answer.Output.GetProperty("label").GetString()!,
            Math.Clamp(answer.Output.GetProperty("confidence").GetDouble(), 0, 1),
            answer.Usage,
            answer.Model);
    }

    public async Task<Judgement> JudgeAsync(JudgeRequest request, ModelSelection model, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(model);

        JsonElement schema = JsonSerializer.SerializeToElement(new
        {
            type = "object",
            properties = new
            {
                reasoning = new { type = "string", description = "Why, briefly, citing the response." },
                score = new { type = "number", description = "How well the response meets the criteria, from 0 to 1." },
                passed = new { type = "boolean", description = "Whether the response meets the criteria." },
            },
            required = judgementFields,
            additionalProperties = false,
        });
        string task = $"Criteria:\n{request.Criteria}\n\nInput:\n{request.Input}\n\nResponse to judge:\n{request.Output}"
            + (request.Reference is null ? "" : $"\n\nReference:\n{request.Reference}");
        StructuredAnswer answer = await StructuredAsync(
            "You grade an AI assistant's response against the criteria. Judge only what the criteria ask.", task, "judgement", schema, model, cancellationToken).ConfigureAwait(false);

        return new Judgement(
            Math.Clamp(answer.Output.GetProperty("score").GetDouble(), 0, 1),
            answer.Output.GetProperty("passed").GetBoolean(),
            answer.Output.GetProperty("reasoning").GetString()!,
            answer.Usage,
            answer.Model);
    }

    public async Task<Embeddings> EmbedAsync(EmbeddingRequest request, ModelSelection model, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(model);

        EmbeddingGenerationOptions options = new() { Dimensions = request.Dimensions };
        OpenAIEmbeddingCollection embeddings = await CallAsync(
            () => client.GetEmbeddingClient(model.Model).GenerateEmbeddingsAsync(request.Inputs, options, cancellationToken), cancellationToken).ConfigureAwait(false);

        return new Embeddings(
            [.. embeddings.OrderBy(embedding => embedding.Index).Select(embedding => embedding.ToFloats())],
            new InferenceUsage(embeddings.Usage?.InputTokenCount ?? 0, 0),
            embeddings.Model is { Length: > 0 } served ? served : model.Model);
    }

    public async Task<AdapterHealth> CheckHealthAsync(CancellationToken cancellationToken)
    {
        try
        {
            await client.GetOpenAIModelClient().GetModelsAsync(cancellationToken).ConfigureAwait(false);

            return AdapterHealth.Healthy;
        }
        catch (ClientResultException exception)
        {
            return AdapterHealth.Unhealthy(exception.Status == 0 ? $"Azure OpenAI unreachable: {exception.Message}" : $"Azure OpenAI ({exception.Status}): {exception.Message}");
        }
        catch (Azure.Identity.AuthenticationFailedException exception)
        {
            return AdapterHealth.Unhealthy($"Could not sign in to Azure OpenAI: {exception.Message}");
        }
    }

    private async Task<StructuredAnswer> StructuredAsync(string instructions, string input, string schemaName, JsonElement schema, ModelSelection model, CancellationToken cancellationToken)
    {
        SdkChatCompletion completion = await SendAsync(
            model, AzureOpenAIMessages.From(instructions, [Ports.Inference.ChatMessage.User(input)]), Options(model, tools: null, schema, schemaName), cancellationToken).ConfigureAwait(false);

        // A filtered or cut-off answer has no usable value; another provider may still answer it.
        if (StopReason(completion) is ChatStopReason.ContentFiltered or ChatStopReason.MaxTokens || completion.Refusal is { Length: > 0 })
        {
            throw new InferenceUnavailableException($"Azure OpenAI did not complete the structured answer ({completion.FinishReason}).");
        }

        using JsonDocument document = JsonDocument.Parse(AzureOpenAIMessages.ToChatMessage(completion).Text);

        return new StructuredAnswer(document.RootElement.Clone(), Usage(completion), Served(completion, model));
    }

    private static ChatCompletionOptions Options(ModelSelection model, ValueList<PortChatTool>? tools, JsonElement? format, string schemaName = "response")
    {
#pragma warning disable OPENAI001 // Reasoning effort is marked for evaluation in the SDK; the API parameter is stable.
        ChatCompletionOptions options = new()
        {
            MaxOutputTokenCount = model.Parameters.MaxOutputTokens,
            ReasoningEffortLevel = model.Parameters.Effort switch
            {
                InferenceEffort.Low => ChatReasoningEffortLevel.Low,
                InferenceEffort.Medium => ChatReasoningEffortLevel.Medium,
                InferenceEffort.High or InferenceEffort.Max => ChatReasoningEffortLevel.High,
                _ => (ChatReasoningEffortLevel?)null,
            },
        };
#pragma warning restore OPENAI001

        foreach (PortChatTool tool in tools ?? [])
        {
            options.Tools.Add(AzureOpenAIMessages.ToTool(tool));
        }

        if (format is { } schema)
        {
            options.ResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat(
                schemaName, BinaryData.FromString(schema.GetRawText()), jsonSchemaIsStrict: AzureOpenAIMessages.IsStrict(schema));
        }

        return options;
    }

    private async Task<SdkChatCompletion> SendAsync(ModelSelection model, List<SdkChatMessage> messages, ChatCompletionOptions options, CancellationToken cancellationToken) =>
        await CallAsync(() => client.GetChatClient(model.Model).CompleteChatAsync(messages, options, cancellationToken), cancellationToken).ConfigureAwait(false);

    private static async Task<T> CallAsync<T>(Func<Task<ClientResult<T>>> call, CancellationToken cancellationToken)
    {
        try
        {
            return (await call().ConfigureAwait(false)).Value;
        }
        catch (ClientResultException exception) when (exception.Status is 0 or 408 or 429 or >= 500)
        {
            throw new InferenceUnavailableException(
                exception.Status switch
                {
                    0 => $"Azure OpenAI could not be reached: {exception.Message}",
                    429 => $"Azure OpenAI rate limit reached: {exception.Message}",
                    _ => $"Azure OpenAI is unavailable ({exception.Status}): {exception.Message}",
                },
                exception);
        }
        catch (ClientResultException exception) when (exception.Status == 400 && IsContentFilter(exception))
        {
            throw new InferenceUnavailableException("Azure OpenAI's content filter declined the prompt.", exception);
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            // Not the caller's cancellation, so the request timed out.
            throw new InferenceUnavailableException("Azure OpenAI did not answer in time.", exception);
        }
    }

    private static bool IsContentFilter(ClientResultException exception) =>
        exception.GetRawResponse()?.Content.ToString().Contains("\"content_filter\"", StringComparison.Ordinal) == true;

    private static ChatStopReason StopReason(SdkChatCompletion completion) =>
        completion.FinishReason switch
        {
            ChatFinishReason.ToolCalls or ChatFinishReason.FunctionCall => ChatStopReason.ToolCalls,
            ChatFinishReason.Length => ChatStopReason.MaxTokens,
            ChatFinishReason.ContentFilter => ChatStopReason.ContentFiltered,
            _ => ChatStopReason.Completed,
        };

    private static InferenceUsage Usage(SdkChatCompletion completion) =>
        completion.Usage is { } usage ? new InferenceUsage(usage.InputTokenCount, usage.OutputTokenCount) : InferenceUsage.None;

    private static string Served(SdkChatCompletion completion, ModelSelection requested) =>
        completion.Model is { Length: > 0 } served ? served : requested.Model;

    private sealed record StructuredAnswer(JsonElement Output, InferenceUsage Usage, string Model);
}
