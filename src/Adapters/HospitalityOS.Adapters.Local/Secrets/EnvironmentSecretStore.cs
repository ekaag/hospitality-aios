using HospitalityOS.Ports.Bindings;
using HospitalityOS.Ports.Secrets;
using Microsoft.Extensions.Configuration;

namespace HospitalityOS.Adapters.Local.Secrets;

/// <summary>
/// Secrets from environment variables, for local runs and CI: the secret <c>anthropic-api-key</c> is the variable
/// <c>ANTHROPIC_API_KEY</c>, after an optional prefix. An empty variable is no secret.
/// </summary>
internal sealed class EnvironmentSecretStore(string prefix) : ISecretStore
{
    public Task<string?> GetAsync(string name, CancellationToken cancellationToken)
    {
        SecretNames.EnsureValid(name);
        cancellationToken.ThrowIfCancellationRequested();

        string? value = Environment.GetEnvironmentVariable(VariableFor(prefix, name));

        return Task.FromResult(string.IsNullOrEmpty(value) ? null : value);
    }

    /// <summary>
    /// The variable holding a secret.
    /// </summary>
    public static string VariableFor(string prefix, string name) => prefix + name.ToUpperInvariant().Replace('-', '_');
}

/// <summary>
/// Binds <see cref="ISecretStore"/> to <see cref="EnvironmentSecretStore"/> under the adapter name <c>Environment</c>.
/// Settings: <c>Prefix</c> (optional), put before every variable name.
/// </summary>
internal sealed class EnvironmentSecretStoreFactory : IAdapterFactory<ISecretStore>
{
    public const string Name = "Environment";

    public string AdapterName => Name;

    public ISecretStore Create(IServiceProvider services, IConfiguration settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        return new EnvironmentSecretStore(settings["Prefix"] ?? string.Empty);
    }
}
