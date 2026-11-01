using System.Collections.Concurrent;
using HospitalityOS.Ports.Storage;

namespace HospitalityOS.Adapters.Local.Storage;

/// <summary>
/// Blobs in memory, for tests and runs that keep records in memory too; nothing survives a restart.
/// </summary>
internal sealed class InMemoryBlobStore : IBlobStore
{
    private readonly ConcurrentDictionary<string, Saved> blobs = new(StringComparer.Ordinal);

    public async Task<bool> PutAsync(string key, Stream content, string contentType, CancellationToken cancellationToken)
    {
        BlobKeys.EnsureValid(key);
        ArgumentNullException.ThrowIfNull(content);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentType);

        if (blobs.ContainsKey(key))
        {
            return false;
        }

        using MemoryStream copy = new();
        await content.CopyToAsync(copy, cancellationToken).ConfigureAwait(false);

        return blobs.TryAdd(key, new Saved(copy.ToArray(), contentType));
    }

    public Task<StoredBlob?> OpenAsync(string key, CancellationToken cancellationToken)
    {
        BlobKeys.EnsureValid(key);
        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult(blobs.TryGetValue(key, out Saved? saved)
            ? new StoredBlob(new MemoryStream(saved.Bytes, writable: false), saved.ContentType, saved.Bytes.LongLength)
            : null);
    }

    public Task<bool> ExistsAsync(string key, CancellationToken cancellationToken)
    {
        BlobKeys.EnsureValid(key);
        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult(blobs.ContainsKey(key));
    }

    private sealed record Saved(byte[] Bytes, string ContentType);
}
