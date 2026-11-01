using HospitalityOS.Ports.Bindings;
using HospitalityOS.Ports.Search;
using Microsoft.Extensions.Configuration;

namespace HospitalityOS.Adapters.Azure.Search;

/// <summary>
/// The settings both Azure AI Search adapters take: <c>Endpoint</c>, the service's https URL (e.g.
/// <c>https://search-content-dev-uksouth-srch.search.windows.net</c>), signed in to as the app's identity; and <c>Prefix</c>
/// (optional), put before every index name.
/// </summary>
internal static class AzureSearchSettings
{
    private const string EndpointSetting = "Endpoint";
    private const string PrefixSetting = "Prefix";

    public static AzureSearchIndexes Indexes(IConfiguration settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        return new AzureSearchIndexes(new Uri(settings[EndpointSetting]!, UriKind.Absolute), AzureCredentials.App, settings[PrefixSetting] ?? string.Empty);
    }

    public static IEnumerable<string> Validate(IConfiguration settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        if (!(Uri.TryCreate(settings[EndpointSetting], UriKind.Absolute, out Uri? uri) && uri.Scheme == Uri.UriSchemeHttps))
        {
            yield return $"{EndpointSetting} must be the search service's https URL, not '{settings[EndpointSetting]}'.";
        }
    }
}

/// <summary>
/// Binds <see cref="ISearchIndex"/> to <see cref="AzureSearchIndex"/> under the adapter name <c>AzureAISearch</c>.
/// Settings: see <see cref="AzureSearchSettings"/>.
/// </summary>
internal sealed class AzureSearchIndexFactory : IAdapterFactory<ISearchIndex>
{
    public const string Name = "AzureAISearch";

    public string AdapterName => Name;

    public ISearchIndex Create(IServiceProvider services, IConfiguration settings) => new AzureSearchIndex(AzureSearchSettings.Indexes(settings));

    public IEnumerable<string> ValidateSettings(IConfiguration settings) => AzureSearchSettings.Validate(settings);
}

/// <summary>
/// Binds <see cref="IVectorStore"/> to <see cref="AzureVectorStore"/> under the adapter name <c>AzureAISearch</c>.
/// Settings: see <see cref="AzureSearchSettings"/>.
/// </summary>
internal sealed class AzureVectorStoreFactory : IAdapterFactory<IVectorStore>
{
    public const string Name = "AzureAISearch";

    public string AdapterName => Name;

    public IVectorStore Create(IServiceProvider services, IConfiguration settings) => new AzureVectorStore(AzureSearchSettings.Indexes(settings));

    public IEnumerable<string> ValidateSettings(IConfiguration settings) => AzureSearchSettings.Validate(settings);
}
