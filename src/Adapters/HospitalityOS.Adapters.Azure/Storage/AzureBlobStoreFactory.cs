using Azure.Storage.Blobs;
using HospitalityOS.Ports.Bindings;
using HospitalityOS.Ports.Storage;
using Microsoft.Extensions.Configuration;

namespace HospitalityOS.Adapters.Azure.Storage;

/// <summary>
/// Binds <see cref="IBlobStore"/> to <see cref="AzureBlobStore"/> under the adapter name <c>AzureBlobStorage</c>. Settings:
/// <c>ServiceUri</c> (e.g. <c>https://blobsdevuksouthst.blob.core.windows.net</c>), signed in to as the app's identity, or
/// <c>ConnectionString</c> (Azurite, local runs), exactly one; <c>Container</c> (default <c>blobs</c>).
/// </summary>
internal sealed class AzureBlobStoreFactory : IAdapterFactory<IBlobStore>
{
    public const string Name = "AzureBlobStorage";

    private const string ServiceUriSetting = "ServiceUri";
    private const string ConnectionStringSetting = "ConnectionString";
    private const string ContainerSetting = "Container";
    private const string DefaultContainer = "blobs";

    public string AdapterName => Name;

    public IBlobStore Create(IServiceProvider services, IConfiguration settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        BlobServiceClient service = settings[ConnectionStringSetting] is { Length: > 0 } connectionString
            ? new BlobServiceClient(connectionString)
            : new BlobServiceClient(new Uri(settings[ServiceUriSetting]!, UriKind.Absolute), AzureCredentials.App);

        return new AzureBlobStore(service.GetBlobContainerClient(settings[ContainerSetting] is { Length: > 0 } container ? container : DefaultContainer));
    }

    public IEnumerable<string> ValidateSettings(IConfiguration settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        bool hasUri = !string.IsNullOrWhiteSpace(settings[ServiceUriSetting]);
        bool hasConnectionString = !string.IsNullOrWhiteSpace(settings[ConnectionStringSetting]);

        if (hasUri == hasConnectionString)
        {
            yield return $"Set exactly one of {ServiceUriSetting} (signs in as the app's identity) and {ConnectionStringSetting} (Azurite, local runs).";
        }
        else if (hasUri && !(Uri.TryCreate(settings[ServiceUriSetting], UriKind.Absolute, out Uri? uri) && uri.Scheme == Uri.UriSchemeHttps))
        {
            yield return $"{ServiceUriSetting} must be an absolute https URL, not '{settings[ServiceUriSetting]}'.";
        }
    }
}
