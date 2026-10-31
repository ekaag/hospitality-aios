namespace HospitalityOS.Contracts.Api.Ai;

/// <summary>
/// What a model call in a trace was.
/// </summary>
public enum ModelCallKind
{
    /// <summary>
    /// A chat turn, possibly with tool calls.
    /// </summary>
    Chat,

    /// <summary>
    /// A schema-constrained extraction.
    /// </summary>
    Extraction,

    /// <summary>
    /// A choice of one label for a text, e.g. a guest message's intent.
    /// </summary>
    Classification,
}

/// <summary>
/// Why a chat model stopped generating.
/// </summary>
public enum TraceStopReason
{
    /// <summary>
    /// It finished its answer.
    /// </summary>
    Completed,

    /// <summary>
    /// It stopped to call tools.
    /// </summary>
    ToolCalls,

    /// <summary>
    /// It reached its output limit.
    /// </summary>
    MaxTokens,

    /// <summary>
    /// It declined to answer.
    /// </summary>
    ContentFiltered,
}

/// <summary>
/// Who a message in a trace is from.
/// </summary>
public enum TraceMessageRole
{
    /// <summary>
    /// The guest or caller, or the platform speaking for it (e.g. a correction).
    /// </summary>
    User,

    /// <summary>
    /// The model.
    /// </summary>
    Assistant,

    /// <summary>
    /// Tool results sent back to the model.
    /// </summary>
    Tool,
}

/// <summary>
/// What a part of a message in a trace is.
/// </summary>
public enum TracePartType
{
    /// <summary>
    /// Text (<see cref="TracePart.Text"/>).
    /// </summary>
    Text,

    /// <summary>
    /// A tool call (<see cref="TracePart.CallId"/>, <see cref="TracePart.Name"/>, <see cref="TracePart.Arguments"/>).
    /// </summary>
    ToolCall,

    /// <summary>
    /// A tool result (<see cref="TracePart.CallId"/>, <see cref="TracePart.Text"/>, <see cref="TracePart.IsError"/>).
    /// </summary>
    ToolResult,

    /// <summary>
    /// The model's reasoning, which only its provider can read (<see cref="TracePart.Provider"/>); its content is not kept.
    /// </summary>
    Reasoning,
}

/// <summary>
/// Why a capability call in a trace ran.
/// </summary>
public enum CapabilityCallPurpose
{
    /// <summary>
    /// The platform read it as context before the model was called.
    /// </summary>
    Context,

    /// <summary>
    /// The model called it as a tool.
    /// </summary>
    Tool,

    /// <summary>
    /// The agent's own code called it to act on what the model understood, e.g. creating the service request a guest asked for.
    /// </summary>
    Action,
}

/// <summary>
/// The authorization and policy decision on a capability call.
/// </summary>
public enum AuthorizationDecision
{
    /// <summary>
    /// The agent was allowed to run it.
    /// </summary>
    Allowed,

    /// <summary>
    /// The agent is not allowed to call it (<c>capability.forbidden</c>).
    /// </summary>
    Denied,

    /// <summary>
    /// It needs a confirmation the agent did not have (<c>capability.confirmation_required</c>).
    /// </summary>
    ConfirmationRequired,

    /// <summary>
    /// The model asked for a tool that is not on its allow-list, so nothing ran (<c>tool.unavailable</c>).
    /// </summary>
    NotOnAllowList,

    /// <summary>
    /// The capability does not exist, so no decision was made (<c>capability.unknown</c>).
    /// </summary>
    NotEvaluated,
}
