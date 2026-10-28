using Anthropic;
using HospitalityOS.Ports.Bindings;
using Microsoft.Extensions.Configuration;

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
        IAnthropicClient client = new AnthropicClient().WithOptions(options => options with
        {
            MaxRetries = read.MaxRetries,
            ApiKey = read.ApiKeyVariable is { } variable ? Environment.GetEnvironmentVariable(variable) : options.ApiKey,
            BaseUrl = read.BaseUrl?.ToString() ?? options.BaseUrl,
            Timeout = read.Timeout ?? options.Timeout,
        });

        return (TPort)(object)new AnthropicModel(client, read);
    }

    public IEnumerable<string> ValidateSettings(IConfiguration settings) => AnthropicSettings.Validate(settings);
}
