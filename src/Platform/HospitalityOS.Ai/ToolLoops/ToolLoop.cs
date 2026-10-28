using System.Text.Json;
using HospitalityOS.Ai.Tracing;
using HospitalityOS.Capabilities.Definitions;
using HospitalityOS.Capabilities.Execution;
using HospitalityOS.Contracts.Api.Ai;
using HospitalityOS.Contracts.Serialization;
using HospitalityOS.Ports.Execution;
using HospitalityOS.Ports.Inference;
using HospitalityOS.SharedKernel.Collections;
using HospitalityOS.SharedKernel.Execution;
using HospitalityOS.SharedKernel.Results;

namespace HospitalityOS.Ai.ToolLoops;

/// <summary>
/// Runs tool loops (see <see cref="IToolLoop"/>). Tool calls in one model message run one at a time, in order, so their
/// effects and records are deterministic.
/// </summary>
internal sealed class ToolLoop(
    IInferenceRouter router,
    ICapabilityExecutor executor,
    OperationContext operation,
    ToolLoopCatalog catalog,
    AITraceRecorder recorder) : IToolLoop
{
    public const string UnavailableToolCode = "tool.unavailable";

    public async Task<Result<ToolLoopResult>> RunAsync(ToolLoopRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        ToolLoopDefinition loop = catalog.Find(request.Task)
            ?? throw new InvalidOperationException($"No tool loop is declared for inference task '{request.Task}'; declare it with AddToolLoop.");
        operation.ActAs(Actor.Agent(loop.Task));

        List<ChatMessage> added = [];
        List<ToolCallRecord> toolCalls = [];
        List<InferenceAttempt> attempts = [];
        InferenceUsage usage = InferenceUsage.None;

        for (int round = 0; ; round++)
        {
            ValueList<ChatMessage> messages = [.. request.Messages, .. added];
            string? systemPrompt = null;
            Result<InferenceOutcome<ChatCompletion>> outcome = await router.RunAsync<IChatModel, ChatCompletion>(
                loop.Task,
                (chat, model, prompt, token) =>
                {
                    systemPrompt = SystemPrompt(loop, prompt, request);

                    return chat.CompleteAsync(new ChatRequest(systemPrompt, messages, loop.Tools, request.ResponseSchema), model, token);
                },
                cancellationToken).ConfigureAwait(false);

            if (outcome.IsFailure)
            {
                IReadOnlyList<InferenceAttempt> tried = outcome.Error is InferenceUnavailableError unavailable ? unavailable.Attempts : [];
                recorder.RecordChat(loop.Task, systemPrompt, messages, loop.Tools, request.ResponseSchema is not null, null, tried);

                return outcome.Error;
            }

            ChatCompletion completion = outcome.Value.Value;
            recorder.RecordChat(loop.Task, systemPrompt, messages, loop.Tools, request.ResponseSchema is not null, completion, outcome.Value.Attempts);
            attempts.AddRange(outcome.Value.Attempts);
            usage = new InferenceUsage(usage.InputTokens + completion.Usage.InputTokens, usage.OutputTokens + completion.Usage.OutputTokens);
            added.Add(completion.Message);

            if (completion.StopReason is not ChatStopReason.ToolCalls)
            {
                return new ToolLoopResult(completion.Message, completion.StopReason, ValueList.From(added), ValueList.From(toolCalls), ValueList.From(attempts), usage);
            }

            if (round == loop.MaxToolRounds)
            {
                return AiErrors.ToolRoundLimit(loop.Task, loop.MaxToolRounds);
            }

            ChatToolCall[] calls = [.. completion.Message.Content.OfType<ChatToolCall>()];

            if (calls.Length == 0)
            {
                throw new InvalidOperationException($"The model for inference task '{loop.Task}' stopped to call tools but called none (an adapter defect).");
            }

            List<ChatContent> results = [];

            foreach (ChatToolCall call in calls)
            {
                (ChatToolResult result, ToolCallRecord record) = await CallAsync(loop, call, cancellationToken).ConfigureAwait(false);
                results.Add(result);
                toolCalls.Add(record);
            }

            added.Add(new ChatMessage(ChatRole.Tool, ValueList.From(results)));
        }
    }

    private static string? SystemPrompt(ToolLoopDefinition loop, Prompt? prompt, ToolLoopRequest request)
    {
        IReadOnlyDictionary<string, string> values = request.PromptValues ?? new Dictionary<string, string>();

        if (prompt is null && values.Count > 0)
        {
            throw new ArgumentException($"Inference task '{loop.Task}' has no prompt, so it takes no prompt values.", nameof(request));
        }

        return prompt?.Render(values);
    }

    private static ChatToolResult Failure(ChatToolCall call, string code, string message) =>
        new(call.CallId, JsonSerializer.Serialize(new ToolError(code, message), ContractJson.Options), IsError: true);

    private async Task<(ChatToolResult Result, ToolCallRecord Record)> CallAsync(ToolLoopDefinition loop, ChatToolCall call, CancellationToken cancellationToken)
    {
        if (!loop.Capabilities.TryGetValue(call.Name, out CapabilityDefinition? capability))
        {
            recorder.RecordCapabilityCall(CapabilityCallPurpose.Tool, call.Name, call.CallId, call.Arguments, null, null, UnavailableToolCode);

            return (
                Failure(call, UnavailableToolCode, $"There is no tool named \"{call.Name}\". Use only the tools you were given."),
                new ToolCallRecord(call.CallId, call.Name, null, call.Arguments, null, UnavailableToolCode));
        }

        string ranAs = $"{capability.Name}@{capability.Version}";
        Result<CapabilityResponse> response = await executor
            .ExecuteAsync(new CapabilityRequest(capability.Name, call.Arguments, capability.Version), cancellationToken)
            .ConfigureAwait(false);
        recorder.RecordCapabilityCall(
            CapabilityCallPurpose.Tool,
            ranAs,
            call.CallId,
            call.Arguments,
            response.IsSuccess ? response.Value.InvocationId : null,
            response.IsSuccess ? response.Value.Output : null,
            response.IsFailure ? response.Error.Code : null);

        return response.IsSuccess
            ? (new ChatToolResult(call.CallId, response.Value.Output.GetRawText()), new ToolCallRecord(call.CallId, call.Name, ranAs, call.Arguments, response.Value.InvocationId, null, response.Value.Output))
            : (Failure(call, response.Error.Code, response.Error.Message), new ToolCallRecord(call.CallId, call.Name, ranAs, call.Arguments, null, response.Error.Code));
    }

    private sealed record ToolError(string Code, string Message);
}
