using Azure.Security.KeyVault.Keys;
using Azure.Security.KeyVault.Keys.Cryptography;
using HospitalityOS.Ports.Security;

namespace HospitalityOS.Adapters.Azure.Security;

/// <summary>
/// <see cref="ISigningKey"/> on an Azure Key Vault RSA key: signing is Key Vault's sign operation (the data's SHA-256
/// digest is computed here and signed in the vault), so the private key never leaves it. The key's current version is
/// read once per <c>refreshEvery</c>; after a rotation, new tokens name the new version and tokens signed by the old one
/// stop validating once the cache refreshes.
/// </summary>
internal sealed class AzureKeyVaultSigningKey(KeyClient keys, string keyName, Func<Uri, CryptographyClient> cryptography, TimeSpan refreshEvery, TimeProvider timeProvider)
    : ISigningKey
{
    private readonly Lock gate = new();
    private Current? current;

    public async Task<SigningPublicKey> GetPublicKeyAsync(CancellationToken cancellationToken) =>
        (await CurrentAsync(cancellationToken).ConfigureAwait(false)).PublicKey;

    public async Task<Signature> SignAsync(byte[] data, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(data);
        Current key = await CurrentAsync(cancellationToken).ConfigureAwait(false);
        SignResult result = await key.Client.SignDataAsync(SignatureAlgorithm.RS256, data, cancellationToken).ConfigureAwait(false);
        return new Signature(key.PublicKey.KeyId, result.Signature);
    }

    private async Task<Current> CurrentAsync(CancellationToken cancellationToken)
    {
        DateTimeOffset now = timeProvider.GetUtcNow();

        lock (gate)
        {
            if (current is { } cached && cached.Until > now)
            {
                return cached;
            }
        }

        KeyVaultKey key = (await keys.GetKeyAsync(keyName, cancellationToken: cancellationToken).ConfigureAwait(false)).Value;
        Current fresh = new(PublicKeyOf(key), cryptography(key.Id), now + refreshEvery);

        lock (gate)
        {
            current = fresh;
        }

        return fresh;
    }

    /// <summary>
    /// The key's public half, named by its versioned ID.
    /// </summary>
    public static SigningPublicKey PublicKeyOf(KeyVaultKey key)
    {
        ArgumentNullException.ThrowIfNull(key);

        if (key.KeyType != KeyType.Rsa && key.KeyType != KeyType.RsaHsm)
        {
            throw new InvalidOperationException($"Key {key.Name} is {key.KeyType}; signing needs an RSA key.");
        }

        return new SigningPublicKey(key.Id.ToString(), key.Key.N, key.Key.E);
    }

    private sealed record Current(SigningPublicKey PublicKey, CryptographyClient Client, DateTimeOffset Until);
}
