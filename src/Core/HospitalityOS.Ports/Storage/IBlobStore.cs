namespace HospitalityOS.Ports.Storage;

/// <summary>
/// Files the app keeps (plan §2.1): evidence photos, content assets. A blob is written once under its key and never
/// changes, so a key that names a blob always names the same bytes. Keys are opaque paths the caller builds, tenant first
/// (P10, e.g. <c>tenants/{tenant}/evidence/...</c>); see <see cref="BlobKeys"/>. Bound through <c>Bindings:BlobStore</c>;
/// instances are shared, so adapters must be thread-safe.
/// </summary>
public interface IBlobStore
{
    /// <summary>
    /// Saves a new blob. When the key is already taken, nothing is written and the existing blob is kept.
    /// </summary>
    /// <param name="key">A key <see cref="BlobKeys.IsValid"/> accepts.</param>
    /// <param name="content">The bytes, read to the end.</param>
    /// <param name="contentType">The media type, e.g. <c>image/jpeg</c>.</param>
    /// <param name="cancellationToken">Cancels the write; a cancelled write leaves no blob.</param>
    /// <returns>Whether the blob was written: false when the key was taken.</returns>
    Task<bool> PutAsync(string key, Stream content, string contentType, CancellationToken cancellationToken);

    /// <summary>
    /// Opens a blob for reading; the caller disposes it.
    /// </summary>
    /// <returns>The blob, or null when there is none under the key.</returns>
    Task<StoredBlob?> OpenAsync(string key, CancellationToken cancellationToken);

    /// <summary>
    /// Whether there is a blob under the key.
    /// </summary>
    Task<bool> ExistsAsync(string key, CancellationToken cancellationToken);
}
