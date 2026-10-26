using HospitalityOS.Capabilities.Definitions;
using HospitalityOS.Capabilities.Registry;
using HospitalityOS.UnitTests.Capabilities.TestDoubles;
using Xunit;

namespace HospitalityOS.UnitTests.Capabilities.Registry;

public sealed class CapabilityRegistryTests
{
    private static readonly CapabilityDefinition requestV1 = Definition("GuestService.RequestItem", 1);
    private static readonly CapabilityDefinition requestV2 = Definition("GuestService.RequestItem", 2);
    private static readonly CapabilityDefinition requestV10 = Definition("GuestService.RequestItem", 10);
    private static readonly CapabilityDefinition getHours = Definition("Property.GetOperatingHours", 1);
    private static readonly CapabilityDefinition complete = Definition("HumanAction.Complete", 1);

    private readonly CapabilityRegistry registry = new([requestV10, getHours, requestV1, complete, requestV2]);

    [Fact]
    public void All_IsOrderedByNameThenVersion()
    {
        Assert.Equal([requestV1, requestV2, requestV10, complete, getHours], registry.All);
    }

    [Fact]
    public void Find_ByName_ReturnsTheLatestVersion()
    {
        Assert.Same(requestV10, registry.Find("GuestService.RequestItem"));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(10)]
    public void Find_ByNameAndVersion_ReturnsThatVersion(int version)
    {
        Assert.Equal(version, registry.Find("GuestService.RequestItem", version)?.Version);
    }

    [Theory]
    [InlineData("GuestService.Unknown", null)]
    [InlineData("guestservice.requestitem", null)]
    [InlineData("GuestService.RequestItem", 3)]
    [InlineData("", null)]
    public void Find_UnknownNameOrVersion_ReturnsNull(string name, int? version)
    {
        Assert.Null(registry.Find(name, version));
    }

    [Fact]
    public void Constructor_SameNameAndVersionTwice_Throws()
    {
        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => new CapabilityRegistry([requestV1, getHours, Definition("GuestService.RequestItem", 1)]));

        Assert.Contains("GuestService.RequestItem", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Constructor_NoDefinitions_IsEmpty()
    {
        CapabilityRegistry empty = new([]);

        Assert.Empty(empty.All);
        Assert.Null(empty.Find("GuestService.RequestItem"));
    }

    private static CapabilityDefinition Definition(string name, int version) =>
        CapabilityDefinition.Create(typeof(RequestItemCapability), RequestItemCapability.Descriptor with { Name = name, Version = version });
}
