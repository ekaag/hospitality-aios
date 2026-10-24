namespace HospitalityOS.Ports.Bindings;

/// <summary>
/// Identifies what a port is being resolved for, so configuration can choose a different
/// adapter per operation and per tenant.
/// </summary>
/// <param name="Operation">Operation name, e.g. <c>guest-notifications</c>; <c>null</c> for any operation.</param>
/// <param name="Tenant">Tenant key as used in configuration; <c>null</c> for no tenant override.</param>
public sealed record BindingContext(string? Operation = null, string? Tenant = null)
{
    /// <summary>
    /// Context that selects the port's default binding.
    /// </summary>
    public static BindingContext Default { get; } = new();
}
