using System.Text.Json;

namespace HospitalityOS.Ports.Inference;

/// <summary>
/// A tool the model may call.
/// </summary>
/// <param name="Name">Name the model uses, e.g. <c>Property_GetOperatingHours</c>.</param>
/// <param name="Description">When to use it, for the model.</param>
/// <param name="InputSchema">JSON Schema of its arguments (an object schema).</param>
public sealed record ChatTool(string Name, string Description, JsonElement InputSchema);
