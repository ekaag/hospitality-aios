using System.Text.Json;
using HospitalityOS.Ai.Grounding;
using HospitalityOS.Ai.ToolLoops;
using HospitalityOS.Capabilities.Definitions;
using HospitalityOS.Capabilities.Execution;
using HospitalityOS.Capabilities.Registry;
using HospitalityOS.Contracts.Api.Ai;
using HospitalityOS.Contracts.Capabilities;
using HospitalityOS.Contracts.Serialization;
using HospitalityOS.Domain.Capabilities.AITraces;
using HospitalityOS.Ports.Bindings;
using HospitalityOS.Ports.Execution;
using HospitalityOS.Ports.Ids;
using HospitalityOS.Ports.Inference;
using HospitalityOS.Ports.Tenancy;
using HospitalityOS.SharedKernel.Collections;
using HospitalityOS.SharedKernel.Results;

namespace HospitalityOS.Ai.Tracing;

/// <summary>
/// Collects the scope's AI turn and stages it as an <see cref="AITrace"/> (see <see cref="IAITraceRecorder"/>). Scoped and,
/// like the unit of work, not thread-safe: the AI components record one step at a time.
/// </summary>
internal sealed class AITraceRecorder(
    IAITraceRepository traces,
    OperationContext operation,
    ICurrentTenant currentTenant,
    IBindingResolver<IIdGenerator> idGenerators,
    CapabilityRegistry capabilities,
    TimeProvider timeProvider) : IAITraceRecorder
{
    private readonly List<CapabilityCall> capabilityCalls = [];
    private readonly List<ModelCall> modelCalls = [];
    private readonly List<ReplyVerdict> replies = [];
    private readonly List<SourceReference> retrieved = [];
    private readonly List<ChatMessage> conversation = [];
    private DateTimeOffset? startedAt;
    private bool completed;

    public async Task<AITraceId> CompleteAsync(AITraceCompletion completion, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(completion);
        ArgumentNullException.ThrowIfNull(completion.Trigger);
        EnsureOpen();

        if (completion.Citations.FirstOrDefault(citation => !retrieved.Contains(citation)) is { } unknown)
        {
            throw new InvalidOperationException($"The response cites {unknown.Type} {unknown.Id} version {unknown.Version}, which was not retrieved during the turn.");
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        AITraceBody body = new(
            completion.Trigger.Text,
            ValueList.From(retrieved),
            ValueList.From(capabilityCalls),
            ValueList.From(modelCalls),
            replies.Count == 0 ? null : new GroundingCheck(ValueList.From(replies)),
            new TraceResponse(completion.Response, completion.Citations),
            new TokenUsage(modelCalls.Sum(call => call.Usage?.InputTokens ?? 0), modelCalls.Sum(call => call.Usage?.OutputTokens ?? 0)));

        AITraceId id = new(idGenerators.Resolve().NewId());
        AITrace trace = AITrace.Record(
            id,
            currentTenant.TenantId,
            completion.PropertyId,
            operation.Actor,
            operation.CorrelationId,
            operation.CausationId,
            completion.Trigger.Type,
            completion.Trigger.Id,
            completion.Intent,
            completion.Outcome,
            startedAt ?? now,
            now,
            JsonSerializer.Serialize(body, ContractJson.Options));

        await traces.AddAsync(trace, cancellationToken).ConfigureAwait(false);
        completed = true;

        return id;
    }

    /// <summary>
    /// Records a chat call: the request as sent (the system prompt rendered by the last attempt) and the reply, or
    /// <c>null</c> when no provider could serve it.
    /// </summary>
    public void RecordChat(
        string task,
        string? systemPrompt,
        IReadOnlyList<ChatMessage> messages,
        IReadOnlyList<ChatTool> tools,
        bool structuredOutput,
        ChatCompletion? completion,
        IReadOnlyList<InferenceAttempt> attempts)
    {
        Begin();

        // Each call usually resends the previous call's conversation and reply; keep only what is new.
        bool continues = conversation.Count > 0 && messages.Count >= conversation.Count && messages.Take(conversation.Count).SequenceEqual(conversation);
        IEnumerable<ChatMessage> input = continues ? messages.Skip(conversation.Count) : messages;

        modelCalls.Add(new ModelCall(
            ModelCallKind.Chat,
            task,
            Attempts(attempts),
            systemPrompt,
            continues,
            ValueList.From(input.Select(TraceMapping.Message)),
            ValueList.From(tools.Select(tool => tool.Name)),
            structuredOutput,
            completion is null ? null : TraceMapping.Message(completion.Message),
            completion is null ? null : TraceMapping.StopReason(completion.StopReason),
            completion is null ? null : new TokenUsage(completion.Usage.InputTokens, completion.Usage.OutputTokens),
            completion?.Model));

        conversation.Clear();
        conversation.AddRange(messages);

        if (completion is not null)
        {
            conversation.Add(completion.Message);
        }
    }

    /// <summary>
    /// Records an extraction call: its instructions, input, and output, or <c>null</c> when no provider could serve it.
    /// </summary>
    public void RecordExtraction(string task, string? instructions, string input, Extraction? extraction, IReadOnlyList<InferenceAttempt> attempts)
    {
        Begin();

        modelCalls.Add(new ModelCall(
            ModelCallKind.Extraction,
            task,
            Attempts(attempts),
            instructions,
            ContinuesConversation: false,
            [TraceMapping.Message(ChatMessage.User(input))],
            [],
            StructuredOutput: true,
            extraction is null ? null : TraceMapping.Message(ChatMessage.Assistant(extraction.Output.GetRawText())),
            null,
            extraction is null ? null : new TokenUsage(extraction.Usage.InputTokens, extraction.Usage.OutputTokens),
            extraction?.Model));
    }

    /// <summary>
    /// Records a classification call: its instructions, input, and the label chosen, or <c>null</c> when no provider could
    /// serve it.
    /// </summary>
    public void RecordClassification(string task, string? instructions, string input, Classification? classification, IReadOnlyList<InferenceAttempt> attempts)
    {
        Begin();

        modelCalls.Add(new ModelCall(
            ModelCallKind.Classification,
            task,
            Attempts(attempts),
            instructions,
            ContinuesConversation: false,
            [TraceMapping.Message(ChatMessage.User(input))],
            [],
            StructuredOutput: false,
            classification is null ? null : TraceMapping.Message(ChatMessage.Assistant(classification.Label)),
            null,
            classification is null ? null : new TokenUsage(classification.Usage.InputTokens, classification.Usage.OutputTokens),
            classification?.Model));
    }

    /// <summary>
    /// Records a capability call and the sources its output names.
    /// </summary>
    /// <param name="purpose">Context read or tool call.</param>
    /// <param name="capability"><c>Name@version</c> as it ran, or the tool name when nothing ran.</param>
    /// <param name="callId">The model's tool call ID, for tool calls.</param>
    /// <param name="input">The input sent.</param>
    /// <param name="invocationId">The invocation, when it succeeded.</param>
    /// <param name="output">The output, when it succeeded.</param>
    /// <param name="errorCode">Why it failed, or <c>null</c>.</param>
    public void RecordCapabilityCall(
        CapabilityCallPurpose purpose,
        string capability,
        string? callId,
        JsonElement input,
        Guid? invocationId,
        JsonElement? output,
        string? errorCode)
    {
        Begin();

        capabilityCalls.Add(new CapabilityCall(
            purpose,
            capability,
            callId,
            Classification(capability),
            Authorization(errorCode),
            input.Clone(),
            output?.Clone(),
            invocationId,
            errorCode));

        if (output is { } value)
        {
            retrieved.AddRange(SourceFinder.In(value).Where(source => !retrieved.Contains(source)));
        }
    }

    public void RecordAction(CapabilityRequest request, Result<CapabilityResponse> response) =>
        RecordRun(CapabilityCallPurpose.Action, request, response);

    public void RecordContext(CapabilityRequest request, Result<CapabilityResponse> response) =>
        RecordRun(CapabilityCallPurpose.Context, request, response);

    private void RecordRun(CapabilityCallPurpose purpose, CapabilityRequest request, Result<CapabilityResponse> response)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(response);

        RecordCapabilityCall(
            purpose,
            response.IsSuccess ? $"{response.Value.Name}@{response.Value.Version}" : request.Version is { } version ? $"{request.Name}@{version}" : request.Name,
            null,
            request.Input,
            response.IsSuccess ? response.Value.InvocationId : null,
            response.IsSuccess ? response.Value.Output : null,
            response.IsFailure ? response.Error.Code : null);
    }

    /// <summary>
    /// Records whether a grounded reply was accepted.
    /// </summary>
    public void RecordReply(bool accepted, string? rejection)
    {
        Begin();
        replies.Add(new ReplyVerdict(accepted, rejection));
    }

    private static ValueList<ModelAttempt> Attempts(IReadOnlyList<InferenceAttempt> attempts) =>
        ValueList.From(attempts.Select(attempt => new ModelAttempt(
            attempt.Provider,
            attempt.Model,
            attempt.Prompt is { } prompt ? new PromptVersionData(prompt.Name, prompt.Number, prompt.Hash) : null,
            attempt.Duration.TotalMilliseconds,
            attempt.Failure)));

    private static AuthorizationDecision Authorization(string? errorCode) => errorCode switch
    {
        CapabilityErrors.ForbiddenCode => AuthorizationDecision.Denied,
        CapabilityErrors.ConfirmationRequiredCode => AuthorizationDecision.ConfirmationRequired,
        ToolLoop.UnavailableToolCode => AuthorizationDecision.NotOnAllowList,
        CapabilityErrors.UnknownCode => AuthorizationDecision.NotEvaluated,

        // Every later check (idempotency, input, the capability itself) runs only once the call was allowed.
        _ => AuthorizationDecision.Allowed,
    };

    private AuditClassification? Classification(string capability)
    {
        int at = capability.LastIndexOf('@');

        return at > 0
            && int.TryParse(capability.AsSpan(at + 1), System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out int version)
            && capabilities.Find(capability[..at], version) is CapabilityDefinition definition
                ? definition.AuditClassification
                : null;
    }

    private void Begin()
    {
        EnsureOpen();
        startedAt ??= timeProvider.GetUtcNow();
    }

    private void EnsureOpen()
    {
        if (completed)
        {
            throw new InvalidOperationException("This scope's AI turn was already completed; a scope records one turn.");
        }
    }
}
