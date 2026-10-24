using HospitalityOS.Bindings;
using HospitalityOS.Ports.Bindings;
using HospitalityOS.UnitTests.Bindings.TestDoubles;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace HospitalityOS.UnitTests.Bindings;

public sealed class BindingResolverTests
{
    [Fact]
    public void Resolve_WithOperationBindings_UsesConfiguredAdapterPerOperation()
    {
        using ServiceProvider provider = BindingTestHost.Build(
            new Dictionary<string, string?>
            {
                ["Bindings:Greeter:Default:Adapter"] = "English",
                ["Bindings:Greeter:Operations:welcome:Adapter"] = "English",
                ["Bindings:Greeter:Operations:vip-welcome:Adapter"] = "French",
                ["Bindings:Greeter:Operations:vip-welcome:Settings:Punctuation"] = "!",
            },
            GreeterFactory.English(),
            GreeterFactory.French());
        IBindingResolver<IGreeter> resolver = provider.GetRequiredService<IBindingResolver<IGreeter>>();

        string welcome = resolver.Resolve(new BindingContext("welcome")).Greet("Ana");
        string vipWelcome = resolver.Resolve(new BindingContext("vip-welcome")).Greet("Ana");

        Assert.Equal("Hello Ana.", welcome);
        Assert.Equal("Bonjour Ana!", vipWelcome);
    }

    [Fact]
    public void Resolve_WithUnconfiguredOperation_FallsBackToDefault()
    {
        using ServiceProvider provider = BindingTestHost.Build(
            new Dictionary<string, string?>
            {
                ["Bindings:Greeter:Default:Adapter"] = "English",
                ["Bindings:Greeter:Operations:vip-welcome:Adapter"] = "French",
            },
            GreeterFactory.English(),
            GreeterFactory.French());
        IBindingResolver<IGreeter> resolver = provider.GetRequiredService<IBindingResolver<IGreeter>>();

        string greeting = resolver.Resolve(new BindingContext("checkout")).Greet("Ana");

        Assert.Equal("Hello Ana.", greeting);
    }

    [Theory]
    [InlineData("tenant-a", "vip-welcome", "Bonjour Ana?")]
    [InlineData("tenant-a", "welcome", "Bonjour Ana.")]
    [InlineData("tenant-b", "vip-welcome", "Hello Ana!")]
    [InlineData("tenant-b", "welcome", "Hello Ana.")]
    public void Resolve_WithTenantOverrides_PrefersTenantOperationThenTenantDefaultThenOperationThenDefault(
        string tenant,
        string operation,
        string expected)
    {
        using ServiceProvider provider = BindingTestHost.Build(
            new Dictionary<string, string?>
            {
                ["Bindings:Greeter:Default:Adapter"] = "English",
                ["Bindings:Greeter:Operations:vip-welcome:Adapter"] = "English",
                ["Bindings:Greeter:Operations:vip-welcome:Settings:Punctuation"] = "!",
                ["Bindings:Greeter:Tenants:tenant-a:Default:Adapter"] = "French",
                ["Bindings:Greeter:Tenants:tenant-a:Operations:vip-welcome:Adapter"] = "French",
                ["Bindings:Greeter:Tenants:tenant-a:Operations:vip-welcome:Settings:Punctuation"] = "?",
            },
            GreeterFactory.English(),
            GreeterFactory.French());
        IBindingResolver<IGreeter> resolver = provider.GetRequiredService<IBindingResolver<IGreeter>>();

        string greeting = resolver.Resolve(new BindingContext(operation, tenant)).Greet("Ana");

        Assert.Equal(expected, greeting);
    }

    [Fact]
    public void Resolve_SameBindingRepeatedly_CreatesAdapterOnce()
    {
        GreeterFactory english = GreeterFactory.English();
        using ServiceProvider provider = BindingTestHost.Build(
            new Dictionary<string, string?> { ["Bindings:Greeter:Default:Adapter"] = "english" },
            english);
        IBindingResolver<IGreeter> resolver = provider.GetRequiredService<IBindingResolver<IGreeter>>();

        IGreeter first = resolver.Resolve();
        IGreeter second = resolver.Resolve(new BindingContext("unconfigured-operation"));

        Assert.Same(first, second);
        Assert.Equal(1, english.CreatedCount);
    }

    [Fact]
    public void Resolve_WithNoBinding_ThrowsBindingException()
    {
        using ServiceProvider provider = BindingTestHost.Build(new Dictionary<string, string?>(), GreeterFactory.English());
        IBindingResolver<IGreeter> resolver = provider.GetRequiredService<IBindingResolver<IGreeter>>();

        BindingException exception = Assert.Throws<BindingException>(() => resolver.Resolve());

        Assert.Contains("Bindings:Greeter:Default", exception.Message, StringComparison.Ordinal);
    }
}
