using Azure.Identity;
using Azure.Security.KeyVault.Keys;
using HospitalityOS.Adapters.Azure;
using HospitalityOS.PortContractTests.Adapters.Azure.Secrets;
using HospitalityOS.PortContractTests.Security;
using HospitalityOS.Ports.Bindings;
using HospitalityOS.Ports.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace HospitalityOS.PortContractTests.Adapters.Azure.Security;

/// <summary>
/// The Key Vault signing key against a real vault (<see cref="AzureKeyVaultSecretStoreContractTests.VaultVariable"/>; there
/// is no emulator), on an RSA key of its own made for the run and deleted afterwards, as whoever is signed in to the
/// Azure CLI. Skipped without the variable, CI included.
/// </summary>
public sealed class AzureKeyVaultSigningKeyContractTests : SigningKeyContractTests, IAsyncDisposable
{
    private readonly string? vault = Environment.GetEnvironmentVariable(AzureKeyVaultSecretStoreContractTests.VaultVariable);
    private readonly string name = $"contract-{Guid.NewGuid():N}"[..24];
    private KeyClient? owner;

    public async ValueTask DisposeAsync()
    {
        if (owner is not null)
        {
            DeleteKeyOperation deleting = await owner.StartDeleteKeyAsync(name);
            await deleting.WaitForCompletionAsync();
            await owner.PurgeDeletedKeyAsync(name);
        }
    }

    protected override ISigningKey CreateKey()
    {
        Assert.SkipWhen(string.IsNullOrWhiteSpace(vault), $"Set {AzureKeyVaultSecretStoreContractTests.VaultVariable} to a Key Vault you may write to, to run the Key Vault contract tests.");

        owner ??= new KeyClient(new Uri(vault!), new DefaultAzureCredential());
        owner.CreateRsaKey(new CreateRsaKeyOptions(name) { KeySize = 2048 });
        ServiceProvider services = new ServiceCollection().AddAzureAdapters().BuildServiceProvider();
        IConfiguration settings = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["VaultUri"] = vault, ["KeyName"] = name }).Build();

        return services.GetServices<IAdapterFactory<ISigningKey>>().Single(factory => factory.AdapterName == "AzureKeyVault").Create(services, settings);
    }
}
