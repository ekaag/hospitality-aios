using System.Globalization;
using Azure.Security.KeyVault.Secrets;
using HospitalityOS.Ports.Bindings;
using HospitalityOS.Ports.Secrets;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HospitalityOS.Adapters.Azure.Secrets;

/// <summary>
/// Binds <see cref="ISecretStore"/> to <see cref="AzureKeyVaultSecretStore"/> under the adapter name <c>AzureKeyVault</c>,
/// signed in to as the app's identity. Settings: <c>VaultUri</c> (e.g. <c>https://secrets-dev-uksouth-kv.vault.azure.net/</c>);
/// <c>CacheSeconds</c> (default 300; 0 reads the vault every time).
/// </summary>
internal sealed class AzureKeyVaultSecretStoreFactory : IAdapterFactory<ISecretStore>
{
    public const string Name = "AzureKeyVault";

    private const string VaultUriSetting = "VaultUri";
    private const string CacheSecondsSetting = "CacheSeconds";
    private const int DefaultCacheSeconds = 300;

    public string AdapterName => Name;

    public ISecretStore Create(IServiceProvider services, IConfiguration settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        return Create(
            new SecretClient(new Uri(settings[VaultUriSetting]!, UriKind.Absolute), AzureCredentials.App),
            settings,
            services.GetService<TimeProvider>() ?? TimeProvider.System);
    }

    /// <summary>
    /// The store on a given client, e.g. one on a test transport.
    /// </summary>
    public static AzureKeyVaultSecretStore Create(SecretClient client, IConfiguration settings, TimeProvider timeProvider) =>
        new(client, TimeSpan.FromSeconds(ReadCacheSeconds(settings) ?? DefaultCacheSeconds), timeProvider);

    public IEnumerable<string> ValidateSettings(IConfiguration settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        if (!(Uri.TryCreate(settings[VaultUriSetting], UriKind.Absolute, out Uri? uri) && uri.Scheme == Uri.UriSchemeHttps))
        {
            yield return $"{VaultUriSetting} must be the vault's https URL, not '{settings[VaultUriSetting]}'.";
        }

        if (settings[CacheSecondsSetting] is { } seconds && ReadCacheSeconds(settings) is not >= 0)
        {
            yield return $"{CacheSecondsSetting} must be a whole number of seconds, 0 or more, not '{seconds}'.";
        }
    }

    private static int? ReadCacheSeconds(IConfiguration settings) =>
        int.TryParse(settings[CacheSecondsSetting], NumberStyles.Integer, CultureInfo.InvariantCulture, out int seconds) ? seconds : null;
}
