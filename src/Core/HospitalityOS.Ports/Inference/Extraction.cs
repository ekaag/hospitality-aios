using System.Text.Json;

namespace HospitalityOS.Ports.Inference;

/// <summary>
/// What a structured extractor read.
/// </summary>
/// <param name="Output">The extracted JSON value.</param>
/// <param name="Usage">Tokens used.</param>
/// <param name="Model">The model that answered.</param>
public sealed record Extraction(JsonElement Output, InferenceUsage Usage, string Model);
