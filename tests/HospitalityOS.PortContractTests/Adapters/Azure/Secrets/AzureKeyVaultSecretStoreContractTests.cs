using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using HospitalityOS.Adapters.Azure;
using HospitalityOS.PortContractTests.Secrets;
using HospitalityOS.Ports.Bindings;
using HospitalityOS.Ports.Secrets;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace HospitalityOS.PortContractTests.Adapters.Azure.Secrets;

/// <summary>
/// The Key Vault store against a real vault, which <see cref="VaultVariable"/> names (there is no Microsoft emulator), as
/// whoever is signed in to the Azure CLI, who must be allowed to set, read, and delete secrets there. The secrets it adds
/// are deleted and purged afterwards. Without the variable, the tests are skipped, CI included, until a test vault is
/// deployed.
/// </summary>
public sealed class AzureKeyVaultSecretStoreContractTests : SecretStoreContractTests
{
    /// <summary>
    /// The test vault's URI, e.g. <c>https://secrets-dev-uksouth-kv.vault.azure.net/</c>.
    /// </summary>
    public const string VaultVariable = "HOSPITALITYOS_KEYVAULT";

    private readonly string? vault = Environment.GetEnvironmentVariable(VaultVariable);
    private readonly List<string> seeded = [];
    private SecretClient? owner;

    protected override (ISecretStore Store, IAsyncDisposable Owner) CreateStore()
    {
        Assert.SkipWhen(string.IsNullOrWhiteSpace(vault), $"Set {VaultVariable} to a Key Vault you may write to, to run the Key Vault contract tests.");

        owner = new SecretClient(new Uri(vault!), new DefaultAzureCredential());
        ServiceProvider services = new ServiceCollection().AddAzureAdapters().BuildServiceProvider();
        IAdapterFactory<ISecretStore> factory = services.GetServices<IAdapterFactory<ISecretStore>>().Single(candidate => candidate.AdapterName == "AzureKeyVault");
        IConfiguration settings = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["VaultUri"] = vault, ["CacheSeconds"] = "0" })
            .Build();

        return (factory.Create(services, settings), new Cleanup(owner, seeded, services));
    }

    protected override async Task SeedAsync(string name, string value)
    {
        seeded.Add(name);
        await owner!.SetSecretAsync(name, value, TestContext.Current.CancellationToken);
    }

    private sealed class Cleanup(SecretClient client, List<string> seeded, ServiceProvider services) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            foreach (string name in seeded)
            {
                DeleteSecretOperation deleting = await client.StartDeleteSecretAsync(name);
                await deleting.WaitForCompletionAsync();
                await client.PurgeDeletedSecretAsync(name);
            }

            await services.DisposeAsync();
        }
    }
}
