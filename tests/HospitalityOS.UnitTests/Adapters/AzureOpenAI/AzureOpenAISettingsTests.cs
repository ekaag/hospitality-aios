using HospitalityOS.Adapters.AzureOpenAI;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace HospitalityOS.UnitTests.Adapters.AzureOpenAI;

public sealed class AzureOpenAISettingsTests
{
    [Theory]
    [InlineData("https://inference-chat-dev-uksouth-oai.openai.azure.com/")]
    [InlineData("https://inference-chat-dev-uksouth-oai.openai.azure.com")]
    public void Read_TheResourcesEndpoint_CallsItsV1Api(string endpoint)
    {
        AzureOpenAISettings settings = AzureOpenAISettings.Read(Settings(("Endpoint", endpoint)));

        Assert.Empty(AzureOpenAISettings.Validate(Settings(("Endpoint", endpoint))));
        Assert.Equal(new Uri("https://inference-chat-dev-uksouth-oai.openai.azure.com/openai/v1/"), settings.ApiBase);
        Assert.Equal((AzureOpenAISettings.DefaultMaxRetries, (TimeSpan?)null), (settings.MaxRetries, settings.Timeout));
    }

    [Fact]
    public void Read_RetriesAndTimeout()
    {
        AzureOpenAISettings settings = AzureOpenAISettings.Read(Settings(("Endpoint", "https://x.openai.azure.com/"), ("MaxRetries", "4"), ("TimeoutSeconds", "30")));

        Assert.Equal((4, (TimeSpan?)TimeSpan.FromSeconds(30)), (settings.MaxRetries, settings.Timeout));
    }

    [Theory]
    [InlineData(null, "Endpoint")]
    [InlineData("http://x.openai.azure.com/", "Endpoint")]
    [InlineData("https://x.openai.azure.com/openai/v1/", "Endpoint")]
    [InlineData("x.openai.azure.com", "Endpoint")]
    public void Validate_RejectsAnythingButTheResourcesHttpsUrl(string? endpoint, string setting)
    {
        string problem = Assert.Single(AzureOpenAISettings.Validate(Settings(("Endpoint", endpoint))));

        Assert.StartsWith(setting, problem, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("MaxRetries", "11")]
    [InlineData("MaxRetries", "-1")]
    [InlineData("TimeoutSeconds", "0")]
    [InlineData("TimeoutSeconds", "soon")]
    public void Validate_RejectsRetriesAndTimeoutsOutOfRange(string setting, string value)
    {
        string problem = Assert.Single(AzureOpenAISettings.Validate(Settings(("Endpoint", "https://x.openai.azure.com/"), (setting, value))));

        Assert.StartsWith(setting, problem, StringComparison.Ordinal);
    }

    private static IConfiguration Settings(params (string Key, string? Value)[] values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values.Select(value => new KeyValuePair<string, string?>(value.Key, value.Value))).Build();
}
