using Azure.Storage.Blobs;
using HospitalityOS.Adapters.Azure;
using HospitalityOS.PortContractTests.Storage;
using HospitalityOS.Ports.Bindings;
using HospitalityOS.Ports.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace HospitalityOS.PortContractTests.Adapters.Azure.Storage;

/// <summary>
/// The Azure Blob Storage store, created the way bindings create it, on a container of its own in the storage account
/// <see cref="ConnectionVariable"/> names (CI runs Azurite; locally, start it with <c>tests/emulators/azurite</c>). Only
/// tests create containers; in Azure the container comes from IaC. Without the variable, the tests are skipped.
/// </summary>
public sealed class AzureBlobStoreContractTests : BlobStoreContractTests
{
    /// <summary>
    /// A storage connection string, e.g. Azurite's <c>UseDevelopmentStorage=true</c>.
    /// </summary>
    public const string ConnectionVariable = "HOSPITALITYOS_AZURITE";

    /// <summary>
    /// Set where storage must be there (CI), so a missing one fails the tests instead of skipping them.
    /// </summary>
    private const string RequiredVariable = "HOSPITALITYOS_AZURITE_REQUIRED";

    private readonly string? connectionString = Environment.GetEnvironmentVariable(ConnectionVariable);
    private readonly string container = $"contract-{Guid.NewGuid():N}";

    private static bool Required => string.Equals(Environment.GetEnvironmentVariable(RequiredVariable), "true", StringComparison.OrdinalIgnoreCase);

    protected override (IBlobStore Store, IAsyncDisposable Owner) CreateStore()
    {
        Assert.SkipWhen(string.IsNullOrWhiteSpace(connectionString) && !Required, $"Set {ConnectionVariable} to a storage account to run the Blob Storage contract tests.");
        Assert.False(string.IsNullOrWhiteSpace(connectionString), $"{RequiredVariable} is set, so {ConnectionVariable} must name a storage account.");

        BlobContainerClient containerClient = new BlobServiceClient(connectionString).GetBlobContainerClient(container);
        containerClient.CreateIfNotExists(cancellationToken: TestContext.Current.CancellationToken);

        ServiceProvider services = new ServiceCollection().AddAzureAdapters().BuildServiceProvider();
        IAdapterFactory<IBlobStore> factory = services.GetServices<IAdapterFactory<IBlobStore>>().Single(candidate => candidate.AdapterName == "AzureBlobStorage");
        IConfiguration settings = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionString"] = connectionString, ["Container"] = container })
            .Build();

        return (factory.Create(services, settings), new Owner(containerClient, services));
    }

    private sealed class Owner(BlobContainerClient container, ServiceProvider services) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            await services.DisposeAsync();
            await container.DeleteIfExistsAsync();
        }
    }
}
