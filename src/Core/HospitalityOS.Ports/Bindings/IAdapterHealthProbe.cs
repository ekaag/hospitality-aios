namespace HospitalityOS.Ports.Bindings;

/// <summary>
/// Optionally implemented by adapters that can check their own connectivity.
/// Binding health checks call it for every configured binding.
/// </summary>
public interface IAdapterHealthProbe
{
    /// <summary>
    /// Checks whether the adapter can currently serve requests.
    /// </summary>
    /// <param name="cancellationToken">Cancels the check.</param>
    /// <returns>The adapter's health.</returns>
    Task<AdapterHealth> CheckHealthAsync(CancellationToken cancellationToken);
}
