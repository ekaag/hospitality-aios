using HospitalityOS.Ports.Secrets;
using Xunit;

namespace HospitalityOS.PortContractTests.Secrets;

/// <summary>
/// What every <see cref="ISecretStore"/> adapter must do: give a secret's value by name, nothing for a name with no
/// secret, and refuse names that would not work on every store. Each test uses names of its own.
/// </summary>
public abstract class SecretStoreContractTests : IAsyncDisposable
{
    private readonly Lazy<(ISecretStore Store, IAsyncDisposable Owner)> created;
    private readonly string suffix = Guid.NewGuid().ToString("N")[..12];

    protected SecretStoreContractTests()
    {
        created = new Lazy<(ISecretStore, IAsyncDisposable)>(CreateStore);
    }

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    private ISecretStore Store => created.Value.Store;

    [Fact]
    public async Task Get_ASecret_GivesItsValue()
    {
        _ = Store;
        await SeedAsync(Name("anthropic-api-key"), "sk-ant-test-1234");
        await SeedAsync(Name("pms-client-secret"), "another value");

        Assert.Equal("sk-ant-test-1234", await Store.GetAsync(Name("anthropic-api-key"), Cancellation));
        Assert.Equal("another value", await Store.GetAsync(Name("pms-client-secret"), Cancellation));
    }

    [Fact]
    public async Task Get_NoSuchSecret_GivesNothing()
    {
        Assert.Null(await Store.GetAsync(Name("never-set"), Cancellation));
    }

    [Theory]
    [InlineData("Upper-Case")]
    [InlineData("under_score")]
    [InlineData("double--hyphen")]
    [InlineData("-leading")]
    [InlineData("")]
    public async Task AnInvalidName_IsRefused(string name)
    {
        await Assert.ThrowsAsync<ArgumentException>(() => Store.GetAsync(name, Cancellation));
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
    /// The adapter under test, and what disposes it (and removes what <see cref="SeedAsync"/> added) after the test.
    /// Called on first use in a test, so it may skip the test.
    /// </summary>
    protected abstract (ISecretStore Store, IAsyncDisposable Owner) CreateStore();

    /// <summary>
    /// Adds a secret the way its store's owners would (environment, vault), never through the port.
    /// </summary>
    protected abstract Task SeedAsync(string name, string value);

    private string Name(string name) => $"{name}-{suffix}";
}
