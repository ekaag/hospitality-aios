using HospitalityOS.Adapters.Local;
using HospitalityOS.PortContractTests.Secrets;
using HospitalityOS.Ports.Bindings;
using HospitalityOS.Ports.Secrets;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HospitalityOS.PortContractTests.Adapters.Local;

/// <summary>
/// The environment-variable secret store, created the way bindings create it, with a prefix of its own so tests running
/// at once never see each other's variables; the variables it sets are cleared afterwards.
/// </summary>
public sealed class EnvironmentSecretStoreContractTests : SecretStoreContractTests
{
    private readonly string prefix = $"HOSPITALITYOS_CONTRACT_{Guid.NewGuid():N}_".ToUpperInvariant();
    private readonly List<string> variables = [];

    protected override (ISecretStore Store, IAsyncDisposable Owner) CreateStore()
    {
        ServiceProvider services = new ServiceCollection().AddLocalAdapters().BuildServiceProvider();
        IAdapterFactory<ISecretStore> factory = services.GetServices<IAdapterFactory<ISecretStore>>().Single(candidate => candidate.AdapterName == "Environment");
        IConfiguration settings = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Prefix"] = prefix }).Build();

        return (factory.Create(services, settings), new Cleanup(variables, services));
    }

    protected override Task SeedAsync(string name, string value)
    {
        string variable = prefix + name.ToUpperInvariant().Replace('-', '_');
        variables.Add(variable);
        Environment.SetEnvironmentVariable(variable, value);

        return Task.CompletedTask;
    }

    private sealed class Cleanup(List<string> variables, ServiceProvider services) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            foreach (string variable in variables)
            {
                Environment.SetEnvironmentVariable(variable, null);
            }

            await services.DisposeAsync();
        }
    }
}
