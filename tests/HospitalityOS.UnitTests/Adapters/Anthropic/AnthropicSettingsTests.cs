using HospitalityOS.Adapters.Anthropic;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace HospitalityOS.UnitTests.Adapters.Anthropic;

public sealed class AnthropicSettingsTests
{
    [Fact]
    public void Validate_SensibleSettings_Pass_AndReadAsGiven()
    {
        IConfiguration settings = Settings(("ApiKeyVariable", "ANTHROPIC_API_KEY"), ("BaseUrl", "https://api.anthropic.com"), ("MaxRetries", "3"), ("TimeoutSeconds", "120"), ("RefusalFallback", "off"));

        Assert.Empty(AnthropicSettings.Validate(settings));
        Assert.Equal(
            new AnthropicSettings("ANTHROPIC_API_KEY", new Uri("https://api.anthropic.com"), 3, TimeSpan.FromSeconds(120), false),
            AnthropicSettings.Read(settings));
    }

    [Fact]
    public void Read_NoSettings_UsesTheDefaults_WithTheRefusalFallbackOn()
    {
        Assert.Equal(new AnthropicSettings(null, null, AnthropicSettings.DefaultMaxRetries, null, true), AnthropicSettings.Read(Settings()));
    }

    [Theory]
    [InlineData("ApiKeyVariable", "sk-ant-api03-secret", "never put the key itself in configuration")]
    [InlineData("BaseUrl", "http://api.anthropic.com", "must be an absolute https URL")]
    [InlineData("MaxRetries", "-1", "must be a whole number from 0 to 10")]
    [InlineData("TimeoutSeconds", "0", "must be a whole number from 1 to 3600")]
    [InlineData("RefusalFallback", "always", "must be Default or Off")]
    public void Validate_RefusesBadSettings(string key, string value, string expected)
    {
        Assert.Contains(AnthropicSettings.Validate(Settings((key, value))), problem => problem.Contains(expected, StringComparison.Ordinal));
    }

    private static IConfiguration Settings(params (string Key, string Value)[] values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values.Select(value => new KeyValuePair<string, string?>(value.Key, value.Value))).Build();
}
