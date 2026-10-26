using HospitalityOS.Capabilities;
using HospitalityOS.Capabilities.Definitions;
using HospitalityOS.Capabilities.Registry;
using HospitalityOS.UnitTests.Capabilities.TestDoubles;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace HospitalityOS.UnitTests.Capabilities;

public sealed class DependencyInjectionTests
{
    [Fact]
    public void AddCapability_EveryVersion_IsInTheRegistry()
    {
        ServiceCollection services = new();
        services.AddCapabilities();
        services.AddCapability<RequestItemCapability>();
        services.AddCapability<RequestItemV2Capability>();

        using ServiceProvider provider = services.BuildServiceProvider(validateScopes: true);
        CapabilityRegistry registry = provider.GetRequiredService<CapabilityRegistry>();

        Assert.Equal([1, 2], registry.All.Select(definition => definition.Version));
        Assert.Equal(typeof(RequestItemV2Capability), registry.Find("GuestService.RequestItem")?.CapabilityType);
    }

    [Fact]
    public void AddCapability_SameNameAndVersionTwice_Throws()
    {
        ServiceCollection services = new();
        services.AddCapability<RequestItemCapability>();

        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => services.AddCapability<RequestItemCapability>());

        Assert.Contains("GuestService.RequestItem v1 is already registered", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AddCapability_InvalidDeclaration_ThrowsBeforeRegistering()
    {
        ServiceCollection services = new();

        Assert.Throws<InvalidOperationException>(() => services.AddCapability<MisnamedCapability>());
        Assert.Empty(services);
    }

    [Fact]
    public void AddCapabilities_WithNoCapabilities_GivesAnEmptyRegistry()
    {
        ServiceCollection services = new();
        services.AddCapabilities();

        using ServiceProvider provider = services.BuildServiceProvider(validateScopes: true);

        Assert.Empty(provider.GetRequiredService<CapabilityRegistry>().All);
    }

    private sealed class MisnamedCapability : ICapability<RequestItemInput, RequestItemOutput>
    {
        public static CapabilityDescriptor Descriptor { get; } = RequestItemCapability.Descriptor with { Name = "request_item" };
    }
}
