using System.Globalization;
using Microsoft.Extensions.Configuration;

namespace HospitalityOS.Adapters.AzureOpenAI;

/// <summary>
/// An Azure OpenAI provider's settings (<c>Inference:Providers:{name}:Settings</c>). <c>Endpoint</c> is the resource's
/// https URL (e.g. <c>https://inference-chat-dev-uksouth-oai.openai.azure.com/</c>), signed in to as the app's identity:
/// the resource accepts no keys. A task's model is the name of a deployment in that resource.
/// </summary>
internal sealed record AzureOpenAISettings(Uri Endpoint, int MaxRetries, TimeSpan? Timeout)
{
    public const string EndpointKey = "Endpoint";
    public const string MaxRetriesKey = "MaxRetries";
    public const string TimeoutSecondsKey = "TimeoutSeconds";

    /// <summary>
    /// The SDK's own retry count for rate limits and server errors before the router may fall back.
    /// </summary>
    public const int DefaultMaxRetries = 2;

    /// <summary>
    /// The resource's OpenAI v1 API, which takes deployment names where OpenAI takes model names.
    /// </summary>
    public Uri ApiBase => new(Endpoint, "openai/v1/");

    public static AzureOpenAISettings Read(IConfiguration settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        string endpoint = settings[EndpointKey]!;

        return new AzureOpenAISettings(
            new Uri(endpoint.EndsWith('/') ? endpoint : endpoint + "/", UriKind.Absolute),
            settings[MaxRetriesKey] is { } retries ? int.Parse(retries, CultureInfo.InvariantCulture) : DefaultMaxRetries,
            settings[TimeoutSecondsKey] is { } seconds ? TimeSpan.FromSeconds(int.Parse(seconds, CultureInfo.InvariantCulture)) : null);
    }

    public static IEnumerable<string> Validate(IConfiguration settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        if (!(Uri.TryCreate(settings[EndpointKey], UriKind.Absolute, out Uri? uri) && uri.Scheme == Uri.UriSchemeHttps && uri.AbsolutePath == "/"))
        {
            yield return $"{EndpointKey} must be the Azure OpenAI resource's https URL, e.g. https://inference-chat-dev-uksouth-oai.openai.azure.com/, not '{settings[EndpointKey]}'.";
        }

        if (settings[MaxRetriesKey] is { } retries && (!int.TryParse(retries, NumberStyles.None, CultureInfo.InvariantCulture, out int count) || count > 10))
        {
            yield return $"{MaxRetriesKey} must be a whole number from 0 to 10, not '{retries}'.";
        }

        if (settings[TimeoutSecondsKey] is { } timeout && (!int.TryParse(timeout, NumberStyles.None, CultureInfo.InvariantCulture, out int seconds) || seconds is < 1 or > 3600))
        {
            yield return $"{TimeoutSecondsKey} must be a whole number from 1 to 3600, not '{timeout}'.";
        }
    }
}
