namespace HospitalityOS.Ports.Inference;

/// <summary>
/// A judging call.
/// </summary>
/// <param name="Criteria">What a good response does, e.g. "states the closing time from the source; invents nothing".</param>
/// <param name="Input">What the response was answering.</param>
/// <param name="Output">The response to judge.</param>
/// <param name="Reference">An expected answer or the authoritative source, if any.</param>
public sealed record JudgeRequest(string Criteria, string Input, string Output, string? Reference = null);
