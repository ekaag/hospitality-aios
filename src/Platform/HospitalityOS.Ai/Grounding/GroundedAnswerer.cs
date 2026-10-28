using System.Text.Json;
using System.Text.Json.Nodes;
using HospitalityOS.Ai.ToolLoops;
using HospitalityOS.Ai.Tracing;
using HospitalityOS.Capabilities.Execution;
using HospitalityOS.Contracts.Api.Ai;
using HospitalityOS.Contracts.Capabilities;
using HospitalityOS.Contracts.Serialization;
using HospitalityOS.Ports.Execution;
using HospitalityOS.Ports.Inference;
using HospitalityOS.SharedKernel.Collections;
using HospitalityOS.SharedKernel.Execution;
using HospitalityOS.SharedKernel.Results;

namespace HospitalityOS.Ai.Grounding;

/// <summary>
/// Answers questions from authoritative sources (see <see cref="IGroundedAnswerer"/>).
/// </summary>
internal sealed class GroundedAnswerer(
    IToolLoop loop,
    ICapabilityExecutor executor,
    OperationContext operation,
    AITraceRecorder recorder,
    IEnumerable<GroundedAnsweringRegistration> registrations) : IGroundedAnswerer
{
    public const string ContextVariable = "context";
    public const string ProfileCapability = "Property.GetProperty";
    public const string ContentCapability = "Property.SearchContent";
    public const int ContextCapabilityVersion = 1;

    /// <summary>
    /// Passages of published content put in the context.
    /// </summary>
    public const int ContextPassages = 3;

    /// <summary>
    /// Replies per question: the first, and one after the model is told why it was not accepted.
    /// </summary>
    public const int MaxReplies = 2;

    private static readonly JsonElement replySchema = ContractSchemas.For(typeof(GroundedReply));

    public async Task<Result<GroundedAnswer>> AnswerAsync(GroundedQuestion question, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(question);

        if (!registrations.Any(registration => string.Equals(registration.Task, question.Task, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException($"Inference task '{question.Task}' is not declared with AddGroundedAnswering.");
        }

        string asked = question.Messages.LastOrDefault(message => message.Role == ChatRole.User)?.Text ?? "";

        if (string.IsNullOrWhiteSpace(asked))
        {
            throw new ArgumentException("The conversation has no guest message to answer.", nameof(question));
        }

        if (question.PromptValues?.ContainsKey(ContextVariable) == true)
        {
            throw new ArgumentException($"The '{ContextVariable}' prompt value is filled in by the answerer.", nameof(question));
        }

        operation.ActAs(Actor.Agent(question.Task));

        // Structured first: the property profile, then the published content that best matches the question.
        List<ContextRead> reads = [];
        Result<CapabilityResponse> profile = await ReadAsync(ProfileCapability, new { propertyId = question.PropertyId }, reads, cancellationToken).ConfigureAwait(false);

        if (profile.IsFailure)
        {
            return profile.Error;
        }

        Result<CapabilityResponse> passages = await ReadAsync(
            ContentCapability, new { propertyId = question.PropertyId, query = asked, limit = ContextPassages }, reads, cancellationToken).ConfigureAwait(false);
        JsonElement content = passages.IsSuccess ? passages.Value.Output.GetProperty("results") : JsonSerializer.SerializeToElement(Array.Empty<object>());
        HashSet<SourceReference> retrieved = [.. SourceFinder.In(profile.Value.Output), .. SourceFinder.In(content)];
        Dictionary<string, string> values = new(question.PromptValues ?? new Dictionary<string, string>())
        {
            [ContextVariable] = new JsonObject { ["property"] = JsonNode.Parse(profile.Value.Output.GetRawText()), ["content"] = JsonNode.Parse(content.GetRawText()) }.ToJsonString(),
        };

        List<ChatMessage> conversation = [.. question.Messages];
        List<ChatMessage> added = [];
        List<ToolCallRecord> toolCalls = [];
        List<InferenceAttempt> attempts = [];
        InferenceUsage usage = InferenceUsage.None;
        string? rejection = null;

        for (int reply = 0; reply < MaxReplies; reply++)
        {
            Result<ToolLoopResult> run = await loop.RunAsync(new ToolLoopRequest(question.Task, [.. conversation], values, replySchema), cancellationToken).ConfigureAwait(false);

            if (run.IsFailure)
            {
                return run.Error;
            }

            ToolLoopResult turn = run.Value;
            added.AddRange(turn.Added);
            toolCalls.AddRange(turn.ToolCalls);
            attempts.AddRange(turn.Attempts);
            usage = new InferenceUsage(usage.InputTokens + turn.Usage.InputTokens, usage.OutputTokens + turn.Usage.OutputTokens);
            retrieved.UnionWith(turn.ToolCalls.Where(call => call.Output is not null).SelectMany(call => SourceFinder.In(call.Output!.Value)));

            (GroundedReply? accepted, rejection) = Check(turn, retrieved);
            recorder.RecordReply(accepted is not null, rejection);

            if (accepted is not null)
            {
                return accepted.Status is GroundedReplyStatus.Answered
                    ? Answer(GroundedOutcome.Answered, accepted.Answer, accepted.Citations, null)
                    : Answer(GroundedOutcome.NoAuthoritativeSource, question.NoSourceAnswer, [], null);
            }

            if (turn.StopReason is ChatStopReason.ContentFiltered)
            {
                break;
            }

            ChatMessage correction = ChatMessage.User(
                $"Your reply was not accepted: {rejection} Reply again as JSON matching the schema. Cite only sources from the context "
                + "or from tool results in this conversation, or reply with status Unknown if they do not answer the question.");
            conversation.AddRange([.. turn.Added, correction]);
            added.Add(correction);
        }

        return Answer(GroundedOutcome.NoAuthoritativeSource, question.NoSourceAnswer, [], rejection);

        GroundedAnswer Answer(GroundedOutcome outcome, string text, ValueList<SourceReference> citations, string? why) =>
            new(outcome, text, citations, ValueList.From(retrieved), why, ValueList.From(reads), ValueList.From(added), ValueList.From(toolCalls), ValueList.From(attempts), usage);
    }

    /// <summary>
    /// The reply if it is complete, well formed, and grounded; otherwise why not.
    /// </summary>
    private static (GroundedReply? Reply, string? Rejection) Check(ToolLoopResult turn, HashSet<SourceReference> retrieved)
    {
        if (turn.StopReason is not ChatStopReason.Completed)
        {
            return (null, $"The reply stopped before it was complete ({turn.StopReason}).");
        }

        GroundedReply reply;

        try
        {
            reply = JsonSerializer.Deserialize<GroundedReply>(turn.Answer.Text, ContractJson.Strict) ?? throw new JsonException("The reply is null.");
        }
        catch (JsonException exception)
        {
            return (null, $"The reply is not JSON matching the schema: {exception.Message}");
        }

        if (reply.Status is GroundedReplyStatus.Unknown)
        {
            return (reply, null);
        }

        if (string.IsNullOrWhiteSpace(reply.Answer))
        {
            return (null, "The answer is empty.");
        }

        if (reply.Citations.Count == 0)
        {
            return (null, "An answer must cite at least one source it relies on.");
        }

        SourceReference? unknown = reply.Citations.FirstOrDefault(citation => !retrieved.Contains(citation));

        return unknown is null
            ? (reply, null)
            : (null, $"It cites {unknown.Type} {unknown.Id} version {unknown.Version}, which is not among the sources given or retrieved for this question.");
    }

    private async Task<Result<CapabilityResponse>> ReadAsync(string capability, object input, List<ContextRead> reads, CancellationToken cancellationToken)
    {
        JsonElement sent = JsonSerializer.SerializeToElement(input, ContractJson.Options);
        Result<CapabilityResponse> response = await executor.ExecuteAsync(new CapabilityRequest(capability, sent, ContextCapabilityVersion), cancellationToken).ConfigureAwait(false);
        ContextRead read = new($"{capability}@{ContextCapabilityVersion}", response.IsSuccess ? response.Value.InvocationId : null, response.IsFailure ? response.Error.Code : null);
        reads.Add(read);
        recorder.RecordCapabilityCall(CapabilityCallPurpose.Context, read.Capability, null, sent, read.InvocationId, response.IsSuccess ? response.Value.Output : null, read.ErrorCode);

        return response;
    }
}
