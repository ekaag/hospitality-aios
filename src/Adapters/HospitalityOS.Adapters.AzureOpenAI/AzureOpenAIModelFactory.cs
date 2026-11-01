using System.ClientModel.Primitives;
using Azure.Core;
using Azure.Identity;
using HospitalityOS.Ports.Bindings;
using Microsoft.Extensions.Configuration;
using OpenAI;

namespace HospitalityOS.Adapters.AzureOpenAI;

/// <summary>
/// Binds an inference port to <see cref="AzureOpenAIModel"/> under the adapter name <c>AzureOpenAI</c>, with the
/// provider's <see cref="AzureOpenAISettings"/>. Requests are signed in as the app's identity: in Azure the API's
/// user-assigned managed identity, which <c>AZURE_CLIENT_ID</c> names; locally, whoever is signed in to the Azure CLI.
/// </summary>
internal sealed class AzureOpenAIModelFactory<TPort> : IAdapterFactory<TPort>
    where TPort : class
{
    /// <summary>
    /// The token audience Azure OpenAI's v1 API accepts.
    /// </summary>
    public const string Scope = "https://cognitiveservices.azure.com/.default";

    private static readonly Lazy<TokenCredential> credential = new(() => new DefaultAzureCredential());

    public string AdapterName => AzureOpenAIModel.ProviderName;

    public TPort Create(IServiceProvider services, IConfiguration settings)
    {
        AzureOpenAISettings read = AzureOpenAISettings.Read(settings);

        return (TPort)(object)new AzureOpenAIModel(Client(read, new BearerTokenPolicy(credential.Value, Scope)));
    }

    public IEnumerable<string> ValidateSettings(IConfiguration settings) => AzureOpenAISettings.Validate(settings);

    public static OpenAIClient Client(AzureOpenAISettings settings, AuthenticationPolicy authentication, PipelineTransport? transport = null)
    {
        ArgumentNullException.ThrowIfNull(settings);

        OpenAIClientOptions options = new()
        {
            Endpoint = settings.ApiBase,
            RetryPolicy = new ClientRetryPolicy(settings.MaxRetries),
        };

        if (settings.Timeout is { } timeout)
        {
            options.NetworkTimeout = timeout;
        }

        if (transport is not null)
        {
            options.Transport = transport;
        }

#pragma warning disable OPENAI001 // Token sign-in is marked for evaluation in the SDK; Microsoft's Azure OpenAI guidance uses it.
        return new OpenAIClient(authentication, options);
#pragma warning restore OPENAI001
    }
}
