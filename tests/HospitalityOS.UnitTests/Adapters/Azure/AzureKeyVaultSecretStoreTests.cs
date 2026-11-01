using System.Diagnostics.CodeAnalysis;
using Azure;
using Azure.Core;
using Azure.Security.KeyVault.Secrets;
using HospitalityOS.Adapters.Azure.Secrets;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace HospitalityOS.UnitTests.Adapters.Azure;

/// <summary>
/// The Key Vault store on a stand-in vault (there is no emulator; the contract tests run against a real one when
/// configured): a missing secret is nothing, and values and absences are kept for the cache time, then read again.
/// </summary>
public sealed class AzureKeyVaultSecretStoreTests
{
    private readonly FakeTimeProvider clock = new(new DateTimeOffset(2026, 11, 1, 9, 0, 0, TimeSpan.Zero));
    private readonly StandInVault vault = new();

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Get_MissingSecret_IsNothing()
    {
        Assert.Null(await Store(cacheSeconds: 0).GetAsync("anthropic-api-key", Cancellation));
    }

    [Fact]
    public async Task Get_WithinTheCacheTime_ReadsTheVaultOnce_ThenAgainAfter()
    {
        AzureKeyVaultSecretStore store = Store(cacheSeconds: 300);
        vault.Secrets["anthropic-api-key"] = "first";

        string? before = await store.GetAsync("anthropic-api-key", Cancellation);
        vault.Secrets["anthropic-api-key"] = "rotated";
        clock.Advance(TimeSpan.FromSeconds(299));
        string? cached = await store.GetAsync("anthropic-api-key", Cancellation);
        clock.Advance(TimeSpan.FromSeconds(1));
        string? after = await store.GetAsync("anthropic-api-key", Cancellation);

        Assert.Equal(["first", "first", "rotated"], [before, cached, after]);
        Assert.Equal(2, vault.Reads);
    }

    [Fact]
    public async Task Get_AbsenceIsCachedToo()
    {
        AzureKeyVaultSecretStore store = Store(cacheSeconds: 300);

        await store.GetAsync("pms-client-secret", Cancellation);
        await store.GetAsync("pms-client-secret", Cancellation);

        Assert.Equal(1, vault.Reads);
    }

    [Fact]
    public async Task Get_WithoutCaching_ReadsTheVaultEveryTime()
    {
        AzureKeyVaultSecretStore store = Store(cacheSeconds: 0);
        vault.Secrets["anthropic-api-key"] = "value";

        await store.GetAsync("anthropic-api-key", Cancellation);
        await store.GetAsync("anthropic-api-key", Cancellation);

        Assert.Equal(2, vault.Reads);
    }

    [Theory]
    [InlineData(null, "VaultUri must be the vault's https URL")]
    [InlineData("http://secrets-dev-uksouth-kv.vault.azure.net/", "VaultUri must be the vault's https URL")]
    public void Validate_RefusesAVaultThatIsNotHttps(string? uri, string expected)
    {
        IConfiguration settings = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["VaultUri"] = uri }).Build();

        Assert.Contains(new AzureKeyVaultSecretStoreFactory().ValidateSettings(settings), problem => problem.Contains(expected, StringComparison.Ordinal));
    }

    private AzureKeyVaultSecretStore Store(int cacheSeconds) =>
        AzureKeyVaultSecretStoreFactory.Create(
            vault,
            new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["CacheSeconds"] = cacheSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture) }).Build(),
            clock);

    /// <summary>
    /// A <see cref="SecretClient"/> answering from a dictionary, as the SDK allows for tests, with 404 for names it lacks.
    /// </summary>
    private sealed class StandInVault : SecretClient
    {
        public Dictionary<string, string> Secrets { get; } = [];

        public int Reads { get; private set; }

        public override Task<Response<KeyVaultSecret>> GetSecretAsync(string name, string? version = null, CancellationToken cancellationToken = default)
        {
            Reads++;

            return Secrets.TryGetValue(name, out string? value)
                ? Task.FromResult(Response.FromValue(new KeyVaultSecret(name, value), new StandInResponse(200)))
                : throw new RequestFailedException(404, $"Secret not found: {name}", "SecretNotFound", null);
        }
    }

    private sealed class StandInResponse(int status) : Response
    {
        public override int Status => status;

        public override string ReasonPhrase => string.Empty;

        public override Stream? ContentStream { get; set; }

        public override string ClientRequestId { get; set; } = string.Empty;

        public override void Dispose()
        {
        }

        protected override bool ContainsHeader(string name) => false;

        protected override IEnumerable<HttpHeader> EnumerateHeaders() => [];

        protected override bool TryGetHeader(string name, [NotNullWhen(true)] out string? value)
        {
            value = null;

            return false;
        }

        protected override bool TryGetHeaderValues(string name, [NotNullWhen(true)] out IEnumerable<string>? values)
        {
            values = null;

            return false;
        }
    }
}
