using System.Text.Json;
using Anthropic;
using Anthropic.Exceptions;
using Anthropic.Models.Beta.Messages;
using HospitalityOS.Ports.Bindings;
using HospitalityOS.Ports.Inference;
using HospitalityOS.SharedKernel.Collections;

namespace HospitalityOS.Adapters.Anthropic;

/// <summary>
/// Claude through the Messages API (beta endpoint, for server-side refusal fallback): chat with tool use, and
/// extraction, classification, and judging through structured outputs. Thinking is left to the model's default
/// (adaptive on current models) and its blocks travel in <see cref="ChatReasoning"/>, because the API requires them back
/// unchanged when a tool loop continues. Sampling settings are never sent (rejected by the API version this SDK uses).
/// Rate limits, overload, server errors, network failures, and timeouts that outlast the SDK's retries become
/// <see cref="InferenceUnavailableException"/>, so the router can fall back; rejected requests propagate. Stateless
/// apart from the client, so one instance serves concurrent calls.
/// </summary>
internal sealed class AnthropicModel(IAnthropicClient client, AnthropicSettings settings)
    : IChatModel, IStructuredExtractor, IClassifier, IEvaluationJudge, IAdapterHealthProbe
{
    public const string ProviderName = "Anthropic";
    public const string FallbackBeta = "server-side-fallback-2026-07-01";

    private const int ChatMaxTokens = 16000;
    private const int TaskMaxTokens = 4096;

    private static readonly string[] classificationFields = ["label", "confidence"];
    private static readonly string[] judgementFields = ["reasoning", "score", "passed"];

    public async Task<ChatCompletion> CompleteAsync(ChatRequest request, ModelSelection model, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(model);

        BetaMessage response = await SendAsync(
            Parameters(model, ChatMaxTokens, request.SystemPrompt, AnthropicMessages.From(request.Messages), request.Tools, format: request.ResponseSchema),
            cancellationToken).ConfigureAwait(false);

        return new ChatCompletion(AnthropicMessages.ToChatMessage(response), StopReason(response), Usage(response), Served(response, model));
    }

    public async Task<Extraction> ExtractAsync(ExtractionRequest request, ModelSelection model, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(model);

        StructuredAnswer answer = await StructuredAsync(request.Instructions, request.Input, request.OutputSchema, model, cancellationToken).ConfigureAwait(false);

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
        StructuredAnswer answer = await StructuredAsync(instructions, request.Input, schema, model, cancellationToken).ConfigureAwait(false);

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
            "You grade an AI assistant's response against the criteria. Judge only what the criteria ask.", task, schema, model, cancellationToken).ConfigureAwait(false);

        return new Judgement(
            Math.Clamp(answer.Output.GetProperty("score").GetDouble(), 0, 1),
            answer.Output.GetProperty("passed").GetBoolean(),
            answer.Output.GetProperty("reasoning").GetString()!,
            answer.Usage,
            answer.Model);
    }

    public async Task<AdapterHealth> CheckHealthAsync(CancellationToken cancellationToken)
    {
        if (settings.ApiKeyVariable is { } variable && string.IsNullOrEmpty(Environment.GetEnvironmentVariable(variable)))
        {
            return AdapterHealth.Unhealthy($"The environment variable {variable} holding the API key is not set.");
        }

        try
        {
            await client.Models.List(cancellationToken: cancellationToken).ConfigureAwait(false);

            return AdapterHealth.Healthy;
        }
        catch (AnthropicApiException exception)
        {
            return AdapterHealth.Unhealthy($"Anthropic API: {exception.Message}");
        }
        catch (AnthropicIOException exception)
        {
            return AdapterHealth.Unhealthy($"Anthropic API unreachable: {exception.Message}");
        }
    }

    // Structured outputs constrain the reply to the schema, so the reply text is the JSON value.
    private async Task<StructuredAnswer> StructuredAsync(string instructions, string input, JsonElement schema, ModelSelection model, CancellationToken cancellationToken)
    {
        BetaMessage response = await SendAsync(
            Parameters(model, TaskMaxTokens, instructions, [AnthropicMessages.User(input)], tools: null, format: schema),
            cancellationToken).ConfigureAwait(false);

        // A declined or cut-off answer has no usable value; another provider may still answer it.
        if (StopReason(response) is ChatStopReason.ContentFiltered or ChatStopReason.MaxTokens)
        {
            throw new InferenceUnavailableException($"Claude did not complete the structured answer ({response.StopReason?.Raw()}).");
        }

        using JsonDocument document = JsonDocument.Parse(AnthropicMessages.ToChatMessage(response).Text);

        return new StructuredAnswer(document.RootElement.Clone(), Usage(response), Served(response, model));
    }

    private MessageCreateParams Parameters(
        ModelSelection model,
        int defaultMaxTokens,
        string? system,
        List<BetaMessageParam> messages,
        ValueList<ChatTool>? tools,
        JsonElement? format)
    {
        // Haiku has no server-side fallback.
        bool fallback = settings.RefusalFallback && !model.Model.StartsWith("claude-haiku", StringComparison.OrdinalIgnoreCase);

        MessageCreateParams parameters = new()
        {
            Model = model.Model,
            MaxTokens = model.Parameters.MaxOutputTokens ?? defaultMaxTokens,
            System = system is null ? null : (MessageCreateParamsSystem)system,
            Messages = messages,
            Tools = tools is { Count: > 0 } ? [.. tools.Select(AnthropicMessages.ToTool)] : null,
            OutputConfig = OutputConfig(model.Parameters.Effort, format),
        };

        // Fallbacks is set only when used: assigning null would send "fallbacks": null.
        return fallback ? parameters with { Fallbacks = new BetaFallbacksParam(new Default()), Betas = [FallbackBeta] } : parameters;
    }

    private static BetaOutputConfig? OutputConfig(InferenceEffort? effort, JsonElement? format)
    {
        if (effort is null && format is null)
        {
            return null;
        }

        // Only the settings given: assigning null would send an explicit null.
        BetaOutputConfig config = new();

        if (effort is { } level)
        {
            config = config with
            {
                Effort = level switch
                {
                    InferenceEffort.Low => Effort.Low,
                    InferenceEffort.Medium => Effort.Medium,
                    InferenceEffort.High => Effort.High,
                    _ => Effort.Max,
                },
            };
        }

        return format is { } schema ? config with { Format = new BetaJsonOutputFormat { Schema = AnthropicMessages.ToDictionary(schema) } } : config;
    }

    private async Task<BetaMessage> SendAsync(MessageCreateParams parameters, CancellationToken cancellationToken)
    {
        try
        {
            return await client.Beta.Messages.Create(parameters, cancellationToken).ConfigureAwait(false);
        }
        catch (AnthropicRateLimitException exception)
        {
            throw new InferenceUnavailableException($"Anthropic rate limit reached: {exception.Message}", exception);
        }
        catch (Anthropic5xxException exception)
        {
            throw new InferenceUnavailableException($"Anthropic is unavailable or overloaded: {exception.Message}", exception);
        }
        catch (AnthropicIOException exception)
        {
            throw new InferenceUnavailableException($"Anthropic could not be reached: {exception.Message}", exception);
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            // Not the caller's cancellation, so the request timed out.
            throw new InferenceUnavailableException("Anthropic did not answer in time.", exception);
        }
    }

    private static ChatStopReason StopReason(BetaMessage response) =>
        response.StopReason?.Raw() switch
        {
            "tool_use" => ChatStopReason.ToolCalls,
            "max_tokens" or "model_context_window_exceeded" => ChatStopReason.MaxTokens,
            "refusal" => ChatStopReason.ContentFiltered,
            _ => ChatStopReason.Completed,
        };

    private static InferenceUsage Usage(BetaMessage response) =>
        new(
            (int)(response.Usage.InputTokens + (response.Usage.CacheCreationInputTokens ?? 0) + (response.Usage.CacheReadInputTokens ?? 0)),
            (int)response.Usage.OutputTokens);

    // After a server-side fallback the response names the model that actually answered.
    private static string Served(BetaMessage response, ModelSelection requested) =>
        response.Model.Raw() is { Length: > 0 } served ? served : requested.Model;

    private sealed record StructuredAnswer(JsonElement Output, InferenceUsage Usage, string Model);
}
