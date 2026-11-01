using HospitalityOS.Ports.Storage;

namespace HospitalityOS.Adapters.Local.Storage;

/// <summary>
/// Blobs as files under a root folder, for local runs that keep data between restarts: each blob's bytes under
/// <c>data/</c> and its media type under <c>types/</c>, both at the key's path. Writing the type file claims the key, so
/// of two writers of one key only the first writes; the bytes are written to a temporary file and moved into place, so
/// a blob exists only once it is whole.
/// </summary>
internal sealed class FileSystemBlobStore(string root) : IBlobStore
{
    private readonly string data = Path.Combine(root, "data");
    private readonly string types = Path.Combine(root, "types");

    public async Task<bool> PutAsync(string key, Stream content, string contentType, CancellationToken cancellationToken)
    {
        BlobKeys.EnsureValid(key);
        ArgumentNullException.ThrowIfNull(content);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentType);

        string typePath = PathOf(types, key);
        string dataPath = PathOf(data, key);

        if (!await ClaimAsync(typePath, contentType, cancellationToken).ConfigureAwait(false))
        {
            return false;
        }

        string temporary = $"{dataPath}.{Guid.NewGuid():N}.partial";

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(dataPath)!);
            FileStream file = new(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, useAsync: true);

            await using (file.ConfigureAwait(false))
            {
                await content.CopyToAsync(file, cancellationToken).ConfigureAwait(false);
            }

            File.Move(temporary, dataPath);

            return true;
        }
        catch
        {
            File.Delete(temporary);
            File.Delete(typePath);

            throw;
        }
    }

    public async Task<StoredBlob?> OpenAsync(string key, CancellationToken cancellationToken)
    {
        BlobKeys.EnsureValid(key);

        string dataPath = PathOf(data, key);

        if (!File.Exists(dataPath))
        {
            return null;
        }

        string contentType = await File.ReadAllTextAsync(PathOf(types, key), cancellationToken).ConfigureAwait(false);
        FileStream file = new(dataPath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true);

        return new StoredBlob(file, contentType, file.Length);
    }

    public Task<bool> ExistsAsync(string key, CancellationToken cancellationToken)
    {
        BlobKeys.EnsureValid(key);
        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult(File.Exists(PathOf(data, key)));
    }

    /// <summary>
    /// Writes the type file unless it is already there; false when another writer has the key.
    /// </summary>
    private static async Task<bool> ClaimAsync(string typePath, string contentType, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(typePath)!);

        try
        {
            FileStream file = new(typePath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, useAsync: true);

            await using (file.ConfigureAwait(false))
            {
                StreamWriter writer = new(file);

                await using (writer.ConfigureAwait(false))
                {
                    await writer.WriteAsync(contentType.AsMemory(), cancellationToken).ConfigureAwait(false);
                }
            }

            return true;
        }
        catch (IOException) when (File.Exists(typePath))
        {
            return false;
        }
    }

    private static string PathOf(string folder, string key) => Path.Combine(folder, key.Replace('/', Path.DirectorySeparatorChar));
}
