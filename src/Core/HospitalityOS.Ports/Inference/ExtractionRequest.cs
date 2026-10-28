using System.Text.Json;

namespace HospitalityOS.Ports.Inference;

/// <summary>
/// An extraction call.
/// </summary>
/// <param name="Instructions">What to extract and how (from the prompt registry).</param>
/// <param name="Input">The text to read, e.g. the guest's message.</param>
/// <param name="SchemaName">Short name of the output shape, e.g. <c>service_request</c>.</param>
/// <param name="OutputSchema">JSON Schema the output must match.</param>
public sealed record ExtractionRequest(string Instructions, string Input, string SchemaName, JsonElement OutputSchema);
