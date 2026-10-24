using HospitalityOS.UnitTests.Bindings.TestDoubles;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace HospitalityOS.UnitTests.Bindings;

public sealed class BindingStartupValidatorTests
{
    [Fact]
    public void Validate_WithValidConfiguration_Succeeds()
    {
        using ServiceProvider provider = BindingTestHost.Build(
            new Dictionary<string, string?>
            {
                ["Bindings:Greeter:Default:Adapter"] = "English",
                ["Bindings:Greeter:Operations:vip-welcome:Adapter"] = "French",
                ["Bindings:Greeter:Operations:vip-welcome:Settings:Punctuation"] = "!",
            },
            GreeterFactory.English(),
            GreeterFactory.French());

        Exception? exception = Record.Exception(() => ValidateStartup(provider));

        Assert.Null(exception);
    }

    [Fact]
    public void Validate_WithMissingDefaultBinding_FailsStartup()
    {
        using ServiceProvider provider = BindingTestHost.Build(
            new Dictionary<string, string?> { ["Bindings:Greeter:Operations:welcome:Adapter"] = "English" },
            GreeterFactory.English());

        OptionsValidationException exception = Assert.Throws<OptionsValidationException>(() => ValidateStartup(provider));

        Assert.Contains(exception.Failures, failure => failure.Contains("has no default binding", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_WithUnknownAdapter_FailsStartupAndListsRegisteredAdapters()
    {
        using ServiceProvider provider = BindingTestHost.Build(
            new Dictionary<string, string?>
            {
                ["Bindings:Greeter:Default:Adapter"] = "English",
                ["Bindings:Greeter:Tenants:tenant-a:Operations:welcome:Adapter"] = "Klingon",
            },
            GreeterFactory.English(),
            GreeterFactory.French());

        OptionsValidationException exception = Assert.Throws<OptionsValidationException>(() => ValidateStartup(provider));

        string failure = Assert.Single(exception.Failures);
        Assert.Contains("'Bindings:Greeter:Tenants:tenant-a:Operations:welcome' selects unknown adapter 'Klingon'", failure, StringComparison.Ordinal);
        Assert.Contains("English, French", failure, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_WithMissingAdapterName_FailsStartup()
    {
        using ServiceProvider provider = BindingTestHost.Build(
            new Dictionary<string, string?> { ["Bindings:Greeter:Default:Settings:Punctuation"] = "!" },
            GreeterFactory.English());

        OptionsValidationException exception = Assert.Throws<OptionsValidationException>(() => ValidateStartup(provider));

        Assert.Contains(exception.Failures, failure => failure.Contains("must set 'Adapter'", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_WithInvalidAdapterSettings_FailsStartup()
    {
        using ServiceProvider provider = BindingTestHost.Build(
            new Dictionary<string, string?>
            {
                ["Bindings:Greeter:Default:Adapter"] = "English",
                ["Bindings:Greeter:Default:Settings:Punctuation"] = "!!!",
            },
            GreeterFactory.English());

        OptionsValidationException exception = Assert.Throws<OptionsValidationException>(() => ValidateStartup(provider));

        string failure = Assert.Single(exception.Failures);
        Assert.Contains("Bindings:Greeter:Default:Settings", failure, StringComparison.Ordinal);
        Assert.Contains("single character", failure, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_WithBindingsForUnknownPort_FailsStartup()
    {
        using ServiceProvider provider = BindingTestHost.Build(
            new Dictionary<string, string?>
            {
                ["Bindings:Greeter:Default:Adapter"] = "English",
                ["Bindings:Greter:Default:Adapter"] = "English",
            },
            GreeterFactory.English());

        OptionsValidationException exception = Assert.Throws<OptionsValidationException>(() => ValidateStartup(provider));

        string failure = Assert.Single(exception.Failures);
        Assert.Contains("'Bindings:Greter' configures an unknown port", failure, StringComparison.Ordinal);
    }

    private static void ValidateStartup(ServiceProvider provider)
    {
        provider.GetRequiredService<IStartupValidator>().Validate();
    }
}
