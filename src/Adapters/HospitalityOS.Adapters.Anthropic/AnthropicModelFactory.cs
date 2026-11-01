using Anthropic;
using HospitalityOS.Ports.Bindings;
using HospitalityOS.Ports.Secrets;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HospitalityOS.Adapters.Anthropic;

/// <summary>
/// Binds an inference port to <see cref="AnthropicModel"/> under the adapter name <c>Anthropic</c>, with the provider's
/// <see cref="AnthropicSettings"/>.
/// </summary>
internal sealed class AnthropicModelFactory<TPort> : IAdapterFactory<TPort>
    where TPort : class
{
    public string AdapterName => AnthropicModel.ProviderName;

    public TPort Create(IServiceProvider services, IConfiguration settings)
    {
        AnthropicSettings read = AnthropicSettings.Read(settings);

        IAnthropicClient Client(string? key) => new AnthropicClient().WithOptions(options => options with
        {
            MaxRetries = read.MaxRetries,
            ApiKey = key ?? options.ApiKey,
            BaseUrl = read.BaseUrl?.ToString() ?? options.BaseUrl,
            Timeout = read.Timeout ?? options.Timeout,
        });

        AnthropicClientSource source = read.ApiKeySecret is { } secret
            ? AnthropicClientSource.FromSecret(services.GetRequiredService<IBindingResolver<ISecretStore>>().Resolve(), secret, Client)
            : AnthropicClientSource.Of(Client(read.ApiKeyVariable is { } variable ? Environment.GetEnvironmentVariable(variable) : null));

        return (TPort)(object)new AnthropicModel(source, read);
    }

    public IEnumerable<string> ValidateSettings(IConfiguration settings) => AnthropicSettings.Validate(settings);
}
