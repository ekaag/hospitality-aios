using HospitalityOS.SharedKernel.Collections;

namespace HospitalityOS.Contracts.Capabilities.Properties;

/// <summary>
/// Output of <c>Property.GetPolicy</c>; empty means there is no authoritative policy.
/// </summary>
/// <param name="Policies">Matching policies, subject-specific first.</param>
public sealed record GetPolicyOutput(
    ValueList<PolicyEntry> Policies);
