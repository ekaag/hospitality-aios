using HospitalityOS.Domain.Properties.ContentItems;

namespace HospitalityOS.Integrations.Native.Cms;

/// <summary>
/// The content items of one tenant for one read, and the DI scope that owns them. Created by a factory registered in
/// <see cref="DependencyInjection"/>.
/// </summary>
internal sealed class NativeContentScope(IContentItemRepository contentItems, IAsyncDisposable owner) : IAsyncDisposable
{
    public IContentItemRepository ContentItems { get; } = contentItems;

    public ValueTask DisposeAsync() => owner.DisposeAsync();
}
