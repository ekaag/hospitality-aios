using HospitalityOS.Adapters.Local;
using HospitalityOS.PortContractTests.Storage;
using HospitalityOS.Ports.Bindings;
using HospitalityOS.Ports.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HospitalityOS.PortContractTests.Adapters.Local;

/// <summary>
/// The in-memory blob store, created the way bindings create it.
/// </summary>
public sealed class InMemoryBlobStoreContractTests : BlobStoreContractTests
{
    protected override (IBlobStore Store, IAsyncDisposable Owner) CreateStore() => LocalBlobStores.Create("InMemory", []);
}

/// <summary>
/// The file-system blob store, in a folder of its own that is deleted afterwards.
/// </summary>
public sealed class FileSystemBlobStoreContractTests : BlobStoreContractTests
{
    private readonly string root = Path.Combine(Path.GetTempPath(), $"hospitalityos-blobs-{Guid.NewGuid():N}");

    protected override (IBlobStore Store, IAsyncDisposable Owner) CreateStore()
    {
        (IBlobStore store, IAsyncDisposable services) = LocalBlobStores.Create("FileSystem", new() { ["Root"] = root });

        return (store, new Folder(root, services));
    }

    private sealed class Folder(string root, IAsyncDisposable services) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            await services.DisposeAsync();

            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }
}

internal static class LocalBlobStores
{
    public static (IBlobStore Store, IAsyncDisposable Owner) Create(string adapter, Dictionary<string, string?> settings)
    {
        ServiceProvider services = new ServiceCollection().AddLocalAdapters().BuildServiceProvider();
        IAdapterFactory<IBlobStore> factory = services.GetServices<IAdapterFactory<IBlobStore>>().Single(candidate => candidate.AdapterName == adapter);

        return (factory.Create(services, new ConfigurationBuilder().AddInMemoryCollection(settings).Build()), services);
    }
}
