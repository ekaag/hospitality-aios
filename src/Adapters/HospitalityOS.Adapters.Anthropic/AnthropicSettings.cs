using System.Globalization;
using HospitalityOS.Ports.Secrets;
using Microsoft.Extensions.Configuration;

namespace HospitalityOS.Adapters.Anthropic;

/// <summary>
/// An Anthropic provider's settings (<c>Inference:Providers:{name}:Settings</c>). The API key is never stored here:
/// <c>ApiKeySecret</c> names the secret holding it in the bound <see cref="ISecretStore"/> (a vault in the cloud), or
/// <c>ApiKeyVariable</c> the environment variable; with neither, the SDK finds credentials itself
/// (<c>ANTHROPIC_API_KEY</c>, then an <c>ant auth login</c> profile, then workload identity federation).
/// </summary>
internal sealed record AnthropicSettings(
    string? ApiKeyVariable,
    Uri? BaseUrl,
    int MaxRetries,
    TimeSpan? Timeout,
    bool RefusalFallback,
    string? ApiKeySecret = null)
{
    public const string ApiKeyVariableKey = "ApiKeyVariable";
    public const string ApiKeySecretKey = "ApiKeySecret";
    public const string BaseUrlKey = "BaseUrl";
    public const string MaxRetriesKey = "MaxRetries";
    public const string TimeoutSecondsKey = "TimeoutSeconds";
    public const string RefusalFallbackKey = "RefusalFallback";

    /// <summary>
    /// The SDK's own retry count for rate limits, overload, and server errors before the router may fall back.
    /// </summary>
    public const int DefaultMaxRetries = 2;

    public static AnthropicSettings Read(IConfiguration settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        return new AnthropicSettings(
            settings[ApiKeyVariableKey],
            settings[BaseUrlKey] is { } baseUrl ? new Uri(baseUrl, UriKind.Absolute) : null,
            settings[MaxRetriesKey] is { } retries ? int.Parse(retries, CultureInfo.InvariantCulture) : DefaultMaxRetries,
            settings[TimeoutSecondsKey] is { } seconds ? TimeSpan.FromSeconds(int.Parse(seconds, CultureInfo.InvariantCulture)) : null,
            !string.Equals(settings[RefusalFallbackKey], "Off", StringComparison.OrdinalIgnoreCase),
            settings[ApiKeySecretKey]);
    }

    public static IEnumerable<string> Validate(IConfiguration settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        if (settings[ApiKeyVariableKey] is { } variable && (variable.Length == 0 || variable.StartsWith("sk-", StringComparison.Ordinal)))
        {
            yield return $"{ApiKeyVariableKey} names the environment variable holding the key; never put the key itself in configuration.";
        }

        if (settings[ApiKeySecretKey] is { } secret && (!SecretNames.IsValid(secret) || secret.StartsWith("sk-", StringComparison.Ordinal)))
        {
            yield return $"{ApiKeySecretKey} names the secret holding the key, e.g. anthropic-api-key; never put the key itself in configuration.";
        }

        if (settings[ApiKeySecretKey] is not null && settings[ApiKeyVariableKey] is not null)
        {
            yield return $"Set {ApiKeySecretKey} or {ApiKeyVariableKey}, not both.";
        }

        if (settings[BaseUrlKey] is { } baseUrl && (!Uri.TryCreate(baseUrl, UriKind.Absolute, out Uri? uri) || uri.Scheme != Uri.UriSchemeHttps))
        {
            yield return $"{BaseUrlKey} must be an absolute https URL, not '{baseUrl}'.";
        }

        if (settings[MaxRetriesKey] is { } retries && (!int.TryParse(retries, NumberStyles.None, CultureInfo.InvariantCulture, out int count) || count > 10))
        {
            yield return $"{MaxRetriesKey} must be a whole number from 0 to 10, not '{retries}'.";
        }

        if (settings[TimeoutSecondsKey] is { } timeout && (!int.TryParse(timeout, NumberStyles.None, CultureInfo.InvariantCulture, out int seconds) || seconds is < 1 or > 3600))
        {
            yield return $"{TimeoutSecondsKey} must be a whole number from 1 to 3600, not '{timeout}'.";
        }

        if (settings[RefusalFallbackKey] is { } fallback
            && !string.Equals(fallback, "Default", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(fallback, "Off", StringComparison.OrdinalIgnoreCase))
        {
            yield return $"{RefusalFallbackKey} must be Default or Off, not '{fallback}'.";
        }
    }
}
