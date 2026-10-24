namespace HospitalityOS.Ports.Bindings;

/// <summary>
/// Resolves the adapter instance that serves a port for a given operation and tenant,
/// as selected by configuration (P5). Application code depends on this, never on adapters.
/// </summary>
/// <typeparam name="TPort">The port interface.</typeparam>
public interface IBindingResolver<out TPort>
    where TPort : class
{
    /// <summary>
    /// Resolves the adapter for <paramref name="context"/>. Precedence, most specific first:
    /// tenant + operation, tenant default, operation, port default.
    /// </summary>
    /// <param name="context">The operation and tenant being served.</param>
    /// <returns>The bound adapter instance (cached per binding).</returns>
    TPort Resolve(BindingContext context);

    /// <summary>
    /// Resolves the port's default binding.
    /// </summary>
    /// <returns>The default adapter instance.</returns>
    TPort Resolve() => Resolve(BindingContext.Default);
}
