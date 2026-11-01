using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using HospitalityOS.Ports.Storage;

namespace HospitalityOS.Adapters.Azure.Storage;

/// <summary>
/// <see cref="IBlobStore"/> on one Azure Blob Storage container: a blob key is the blob's name. Writes are conditional
/// on no blob being there (<c>If-None-Match: *</c>), so a key is written once even with several writers. The container is
/// infrastructure (IaC, P12).
/// </summary>
internal sealed class AzureBlobStore(BlobContainerClient container) : IBlobStore
{
    public async Task<bool> PutAsync(string key, Stream content, string contentType, CancellationToken cancellationToken)
    {
        BlobKeys.EnsureValid(key);
        ArgumentNullException.ThrowIfNull(content);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentType);

        BlobUploadOptions options = new()
        {
            HttpHeaders = new BlobHttpHeaders { ContentType = contentType },
            Conditions = new BlobRequestConditions { IfNoneMatch = ETag.All },
        };

        try
        {
            await container.GetBlobClient(key).UploadAsync(content, options, cancellationToken).ConfigureAwait(false);

            return true;
        }
        catch (RequestFailedException exception) when (exception.Status is 409 or 412)
        {
            // BlobAlreadyExists, or the condition failing on a blob written meanwhile.
            return false;
        }
    }

    public async Task<StoredBlob?> OpenAsync(string key, CancellationToken cancellationToken)
    {
        BlobKeys.EnsureValid(key);

        try
        {
            Response<BlobDownloadStreamingResult> download = await container.GetBlobClient(key).DownloadStreamingAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
            BlobDownloadDetails details = download.Value.Details;

            return new StoredBlob(download.Value.Content, details.ContentType, details.ContentLength);
        }
        catch (RequestFailedException exception) when (exception.Status == 404)
        {
            return null;
        }
    }

    public async Task<bool> ExistsAsync(string key, CancellationToken cancellationToken)
    {
        BlobKeys.EnsureValid(key);

        return (await container.GetBlobClient(key).ExistsAsync(cancellationToken).ConfigureAwait(false)).Value;
    }
}
