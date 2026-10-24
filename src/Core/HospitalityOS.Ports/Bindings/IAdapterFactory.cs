using Microsoft.Extensions.Configuration;

namespace HospitalityOS.Ports.Bindings;

/// <summary>
/// Creates instances of one adapter (one technology) for a port. Each adapter project
/// registers its factories as <c>IAdapterFactory&lt;TPort&gt;</c> singletons; configuration then
/// selects a factory by <see cref="AdapterName"/> for each binding (P5).
/// </summary>
/// <typeparam name="TPort">The port interface the adapter implements.</typeparam>
public interface IAdapterFactory<out TPort>
    where TPort : class
{
    /// <summary>
    /// Name used in configuration to select this adapter, e.g. <c>InMemory</c> or <c>AzureServiceBus</c>.
    /// Matched case-insensitively and unique per port.
    /// </summary>
    string AdapterName { get; }

    /// <summary>
    /// Creates an adapter instance for one binding. Called at most once per binding; the instance
    /// is cached and shared, so it must be thread-safe.
    /// </summary>
    /// <param name="services">Service provider for resolving the adapter's own dependencies.</param>
    /// <param name="settings">The binding's <c>Settings</c> section (may be empty).</param>
    /// <returns>The adapter instance.</returns>
    TPort Create(IServiceProvider services, IConfiguration settings);

    /// <summary>
    /// Validates a binding's settings at startup so misconfiguration fails fast.
    /// </summary>
    /// <param name="settings">The binding's <c>Settings</c> section (may be empty).</param>
    /// <returns>Human-readable validation errors; empty when the settings are valid.</returns>
    IEnumerable<string> ValidateSettings(IConfiguration settings) => [];
}
