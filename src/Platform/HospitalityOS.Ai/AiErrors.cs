using HospitalityOS.SharedKernel.Results;

namespace HospitalityOS.Ai;

/// <summary>
/// Expected failures of the AI layer. Like <c>inference.unavailable</c>, each means the model could not produce a usable
/// result, so callers tell the guest honestly they cannot help right now (P4) rather than guess.
/// </summary>
public static class AiErrors
{
    /// <summary>
    /// The model kept calling tools after the loop's last allowed round.
    /// </summary>
    public const string ToolRoundLimitCode = "inference.tool_round_limit";

    /// <summary>
    /// The model's structured output did not match the schema, even after it was told what was wrong.
    /// </summary>
    public const string InvalidOutputCode = "inference.invalid_output";

    internal static Error ToolRoundLimit(string task, int rounds) =>
        Error.Unavailable(ToolRoundLimitCode, $"The model for inference task '{task}' was still calling tools after {rounds} rounds.");

    internal static Error InvalidOutput(string task, string reason) =>
        Error.Unavailable(InvalidOutputCode, $"The model for inference task '{task}' did not produce output matching the schema: {reason}");
}
