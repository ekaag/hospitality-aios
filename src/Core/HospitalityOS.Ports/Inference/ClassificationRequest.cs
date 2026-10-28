using HospitalityOS.SharedKernel.Collections;

namespace HospitalityOS.Ports.Inference;

/// <summary>
/// A classification call.
/// </summary>
/// <param name="Instructions">What the labels mean (from the prompt registry).</param>
/// <param name="Input">The text to classify.</param>
/// <param name="Labels">The labels to choose from; at least two, distinct.</param>
public sealed record ClassificationRequest(string Instructions, string Input, ValueList<string> Labels);
