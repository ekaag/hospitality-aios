using HospitalityOS.SharedKernel.Collections;

namespace HospitalityOS.Ports.Inference;

/// <summary>
/// A rerank call.
/// </summary>
/// <param name="Query">What the guest asked.</param>
/// <param name="Documents">Candidate passages; at least one.</param>
/// <param name="TopN">Most results to return; <c>null</c> for all.</param>
public sealed record RerankRequest(string Query, ValueList<string> Documents, int? TopN = null);
