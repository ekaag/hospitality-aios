namespace HospitalityOS.Ports.Secrets;

/// <summary>
/// Secrets the app reads at run time, such as API keys (plan §2.1, P10), by name: environment variables locally, a vault
/// in the cloud. Configuration names a secret, never holds one. Adapters may cache values for a few minutes, so a
/// rotated secret is seen soon after, not at once. Bound through <c>Bindings:SecretStore</c>; instances are shared, so
/// adapters must be thread-safe.
/// </summary>
public interface ISecretStore
{
    /// <summary>
    /// The secret's current value.
    /// </summary>
    /// <param name="name">A name <see cref="SecretNames.IsValid"/> accepts, e.g. <c>anthropic-api-key</c>.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The value, or null when there is no such secret.</returns>
    Task<string?> GetAsync(string name, CancellationToken cancellationToken);
}
