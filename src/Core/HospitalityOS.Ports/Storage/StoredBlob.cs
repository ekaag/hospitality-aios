namespace HospitalityOS.Ports.Storage;

/// <summary>
/// A blob opened for reading. Disposing it closes the content.
/// </summary>
/// <param name="Content">The bytes, from the start.</param>
/// <param name="ContentType">The media type it was saved with.</param>
/// <param name="Length">Its size in bytes.</param>
public sealed record StoredBlob(Stream Content, string ContentType, long Length) : IAsyncDisposable
{
    /// <inheritdoc />
    public ValueTask DisposeAsync() => Content.DisposeAsync();
}
