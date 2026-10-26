using HospitalityOS.SharedKernel.Collections;

namespace HospitalityOS.Contracts.Capabilities;

/// <summary>
/// Who may invoke a capability (spec §11 <c>authorization</c>). Callers not listed are refused. Tenant and property
/// isolation apply on top of this, whoever the caller is (P10).
/// </summary>
/// <param name="AllowedCallers">The callers allowed, at least one, each once.</param>
public sealed record CapabilityAuthorization(ValueList<CapabilityCaller> AllowedCallers)
{
    /// <summary>
    /// Allows exactly <paramref name="callers"/>.
    /// </summary>
    public static CapabilityAuthorization Allow(params CapabilityCaller[] callers) => new(ValueList.From(callers));

    /// <summary>
    /// Whether <paramref name="caller"/> may invoke the capability.
    /// </summary>
    public bool Allows(CapabilityCaller caller) => AllowedCallers.Contains(caller);
}
