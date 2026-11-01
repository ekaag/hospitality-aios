using System.Security.Cryptography;
using HospitalityOS.Ports.Storage;
using Xunit;

namespace HospitalityOS.PortContractTests.Storage;

/// <summary>
/// What every <see cref="IBlobStore"/> adapter must do: give back exactly what was saved, with its media type and size;
/// write a key once and keep the first blob when it is written again, even by writers racing for it; and answer for
/// keys with nothing under them. Each test writes under keys of its own.
/// </summary>
public abstract class BlobStoreContractTests : IAsyncDisposable
{
    private readonly Lazy<(IBlobStore Store, IAsyncDisposable Owner)> created;
    private readonly string prefix = $"contract/{Guid.NewGuid():N}";

    protected BlobStoreContractTests()
    {
        created = new Lazy<(IBlobStore, IAsyncDisposable)>(CreateStore);
    }

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    private IBlobStore Store => created.Value.Store;

    [Fact]
    public async Task Put_NewKey_IsReadBackExactly()
    {
        byte[] photo = Bytes(2_048, seed: 1);

        bool written = await Store.PutAsync(Key("towels.jpg"), new MemoryStream(photo), "image/jpeg", Cancellation);

        Assert.True(written);
        await AssertBlobAsync(Key("towels.jpg"), photo, "image/jpeg");
        Assert.True(await Store.ExistsAsync(Key("towels.jpg"), Cancellation));
    }

    [Fact]
    public async Task Put_LargeBlob_IsReadBackExactly()
    {
        byte[] scan = Bytes(6 * 1024 * 1024 + 17, seed: 2);

        Assert.True(await Store.PutAsync(Key("evidence/scan.pdf"), new MemoryStream(scan), "application/pdf", Cancellation));

        await AssertBlobAsync(Key("evidence/scan.pdf"), scan, "application/pdf");
    }

    [Fact]
    public async Task Put_TakenKey_WritesNothing_AndKeepsTheFirst()
    {
        byte[] first = Bytes(100, seed: 3);
        await Store.PutAsync(Key("note.txt"), new MemoryStream(first), "text/plain", Cancellation);

        bool written = await Store.PutAsync(Key("note.txt"), new MemoryStream(Bytes(200, seed: 4)), "image/png", Cancellation);

        Assert.False(written);
        await AssertBlobAsync(Key("note.txt"), first, "text/plain");
    }

    [Fact]
    public async Task Put_WritersRacingForAKey_ExactlyOneWrites()
    {
        byte[][] contents = [.. Enumerable.Range(10, 8).Select(seed => Bytes(50_000, seed))];

        bool[] written = await Task.WhenAll(contents.Select(content => Task.Run(() => Store.PutAsync(Key("race.bin"), new MemoryStream(content), "application/octet-stream", Cancellation), Cancellation)));

        int winner = Assert.Single(Enumerable.Range(0, written.Length), index => written[index]);
        await AssertBlobAsync(Key("race.bin"), contents[winner], "application/octet-stream");
    }

    [Fact]
    public async Task Open_KeyWithNothingUnderIt_IsNull()
    {
        await Store.PutAsync(Key("a/b.txt"), new MemoryStream(Bytes(10, seed: 5)), "text/plain", Cancellation);

        Assert.Null(await Store.OpenAsync(Key("a/missing.txt"), Cancellation));
        Assert.Null(await Store.OpenAsync(Key("a"), Cancellation));
        Assert.False(await Store.ExistsAsync(Key("a/missing.txt"), Cancellation));
    }

    [Theory]
    [InlineData("Upper/case")]
    [InlineData("../escape")]
    [InlineData("double//slash")]
    [InlineData("trailing/")]
    [InlineData("back\\slash")]
    public async Task AnInvalidKey_IsRefused(string key)
    {
        await Assert.ThrowsAsync<ArgumentException>(() => Store.PutAsync(key, new MemoryStream(Bytes(1, seed: 6)), "text/plain", Cancellation));
        await Assert.ThrowsAsync<ArgumentException>(() => Store.OpenAsync(key, Cancellation));
    }

    public async ValueTask DisposeAsync()
    {
        if (created.IsValueCreated)
        {
            await created.Value.Owner.DisposeAsync();
        }

        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// The adapter under test, and what disposes it after the test. Called on first use in a test, so it may skip the test.
    /// </summary>
    protected abstract (IBlobStore Store, IAsyncDisposable Owner) CreateStore();

    private string Key(string name) => $"{prefix}/{name}";

    private async Task AssertBlobAsync(string key, byte[] expected, string contentType)
    {
        StoredBlob? blob = await Store.OpenAsync(key, Cancellation);
        Assert.NotNull(blob);

        await using (blob)
        {
            using MemoryStream read = new();
            await blob.Content.CopyToAsync(read, Cancellation);

            Assert.Equal(contentType, blob.ContentType);
            Assert.Equal(expected.LongLength, blob.Length);
            Assert.Equal(SHA256.HashData(expected), SHA256.HashData(read.ToArray()));
        }
    }

    private static byte[] Bytes(int length, int seed)
    {
        byte[] bytes = new byte[length];
        new Random(seed).NextBytes(bytes);

        return bytes;
    }
}
