using HospitalityOS.Ports.Bindings;

namespace HospitalityOS.Bindings;

/// <summary>
/// Health of one configured binding.
/// </summary>
/// <param name="Path">Configuration path of the binding.</param>
/// <param name="Adapter">Adapter name the binding selects.</param>
/// <param name="Health">Result of resolving and probing the adapter.</param>
internal sealed record BindingHealth(string Path, string Adapter, AdapterHealth Health);
