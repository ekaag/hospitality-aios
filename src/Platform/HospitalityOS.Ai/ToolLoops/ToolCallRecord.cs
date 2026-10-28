using System.Text.Json;

namespace HospitalityOS.Ai.ToolLoops;

/// <summary>
/// One tool call a model made in a tool loop and what came of it.
/// </summary>
/// <param name="CallId">The model's call ID.</param>
/// <param name="Tool">The tool name the model used, e.g. <c>Property_GetOperatingHours</c>.</param>
/// <param name="Capability">The capability it ran as, e.g. <c>Property.GetOperatingHours@1</c>; <c>null</c> when the
/// tool was not on the allow-list and nothing ran.</param>
/// <param name="Arguments">The arguments as the model gave them.</param>
/// <param name="InvocationId">The capability invocation that answered it, when it succeeded.</param>
/// <param name="ErrorCode">Why it failed, e.g. <c>capability.invalid_input</c> or <c>tool.unavailable</c>; <c>null</c>
/// when it succeeded.</param>
/// <param name="Output">The capability's output, when it succeeded.</param>
public sealed record ToolCallRecord(string CallId, string Tool, string? Capability, JsonElement Arguments, Guid? InvocationId, string? ErrorCode, JsonElement? Output = null)
{
    /// <summary>
    /// Whether the call succeeded.
    /// </summary>
    public bool Succeeded => ErrorCode is null;
}
