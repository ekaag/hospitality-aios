using Azure.Security.KeyVault.Keys;
using Azure.Security.KeyVault.Keys.Cryptography;
using HospitalityOS.Ports.Bindings;
using HospitalityOS.Ports.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HospitalityOS.Adapters.Azure.Security;

/// <summary>
/// Binds <see cref="ISigningKey"/> to <see cref="AzureKeyVaultSigningKey"/> under the adapter name <c>AzureKeyVault</c>,
/// signed in to as the app's identity, which needs Key Vault Crypto User. Settings: <c>VaultUri</c> (e.g.
/// <c>https://secrets-dev-uksouth-kv.vault.azure.net/</c>) and <c>KeyName</c> (e.g. <c>stay-tokens</c>).
/// </summary>
internal sealed class AzureKeyVaultSigningKeyFactory : IAdapterFactory<ISigningKey>
{
    public const string Name = "AzureKeyVault";
    private const string VaultUriSetting = "VaultUri";
    private const string KeyNameSetting = "KeyName";

    // Rotation shows within this time; reads stay far below the vault's limits.
    private static readonly TimeSpan refreshEvery = TimeSpan.FromMinutes(10);

    public string AdapterName => Name;

    public ISigningKey Create(IServiceProvider services, IConfiguration settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return new AzureKeyVaultSigningKey(
            new KeyClient(new Uri(settings[VaultUriSetting]!, UriKind.Absolute), AzureCredentials.App),
            settings[KeyNameSetting]!,
            id => new CryptographyClient(id, AzureCredentials.App),
            refreshEvery,
            services.GetService<TimeProvider>() ?? TimeProvider.System);
    }

    public IEnumerable<string> ValidateSettings(IConfiguration settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        if (!(Uri.TryCreate(settings[VaultUriSetting], UriKind.Absolute, out Uri? uri) && uri.Scheme == Uri.UriSchemeHttps))
        {
            yield return $"{VaultUriSetting} must be the vault's https URL, not '{settings[VaultUriSetting]}'.";
        }

        if (string.IsNullOrWhiteSpace(settings[KeyNameSetting]))
        {
            yield return $"{KeyNameSetting} must name the vault's RSA key.";
        }
    }
}
