using System.Collections.Concurrent;
using Azure;
using Azure.Security.KeyVault.Secrets;
using HospitalityOS.Ports.Secrets;

namespace HospitalityOS.Adapters.Azure.Secrets;

/// <summary>
/// <see cref="ISecretStore"/> on Azure Key Vault: a secret name is the vault secret's name, and its current version is
/// read. Values, and the absence of a secret, are kept for <c>cacheFor</c>, so calls on every request do not reach the
/// vault's request limits and a rotated secret is seen within that time.
/// </summary>
internal sealed class AzureKeyVaultSecretStore(SecretClient client, TimeSpan cacheFor, TimeProvider timeProvider) : ISecretStore
{
    private readonly ConcurrentDictionary<string, Cached> cache = new(StringComparer.Ordinal);

    public async Task<string?> GetAsync(string name, CancellationToken cancellationToken)
    {
        SecretNames.EnsureValid(name);

        DateTimeOffset now = timeProvider.GetUtcNow();

        if (cache.TryGetValue(name, out Cached? cached) && cached.Until > now)
        {
            return cached.Value;
        }

        string? value;

        try
        {
            value = (await client.GetSecretAsync(name, version: null, cancellationToken).ConfigureAwait(false)).Value.Value;
        }
        catch (RequestFailedException exception) when (exception.Status == 404)
        {
            value = null;
        }

        if (cacheFor > TimeSpan.Zero)
        {
            cache[name] = new Cached(value, now + cacheFor);
        }

        return value;
    }

    private sealed record Cached(string? Value, DateTimeOffset Until);
}
