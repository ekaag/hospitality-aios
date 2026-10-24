namespace HospitalityOS.Bindings.Configuration;

/// <summary>
/// Per-tenant overrides for one port.
/// </summary>
/// <param name="DefaultBinding">The tenant's default binding, if any.</param>
/// <param name="Operations">The tenant's per-operation bindings.</param>
internal sealed record TenantBindingConfiguration(
    AdapterBinding? DefaultBinding,
    IReadOnlyDictionary<string, AdapterBinding> Operations);
