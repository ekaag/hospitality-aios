using HospitalityOS.Ports.Bindings;
using HospitalityOS.Ports.Storage;
using Microsoft.Extensions.Configuration;

namespace HospitalityOS.Adapters.Local.Storage;

/// <summary>
/// Binds <see cref="IBlobStore"/> to <see cref="InMemoryBlobStore"/> under the adapter name <c>InMemory</c>. No settings.
/// </summary>
internal sealed class InMemoryBlobStoreFactory : IAdapterFactory<IBlobStore>
{
    public const string Name = "InMemory";

    public string AdapterName => Name;

    public IBlobStore Create(IServiceProvider services, IConfiguration settings) => new InMemoryBlobStore();
}

/// <summary>
/// Binds <see cref="IBlobStore"/> to <see cref="FileSystemBlobStore"/> under the adapter name <c>FileSystem</c>. Settings:
/// <c>Root</c>, the folder to keep blobs in (relative paths are from the working directory).
/// </summary>
internal sealed class FileSystemBlobStoreFactory : IAdapterFactory<IBlobStore>
{
    public const string Name = "FileSystem";

    private const string RootSetting = "Root";

    public string AdapterName => Name;

    public IBlobStore Create(IServiceProvider services, IConfiguration settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        return new FileSystemBlobStore(Path.GetFullPath(settings[RootSetting] ?? throw new InvalidOperationException($"Set {RootSetting}.")));
    }

    public IEnumerable<string> ValidateSettings(IConfiguration settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        if (string.IsNullOrWhiteSpace(settings[RootSetting]))
        {
            yield return $"{RootSetting} must name the folder to keep blobs in.";
        }
    }
}
