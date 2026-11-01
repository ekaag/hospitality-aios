using HospitalityOS.Adapters.Azure.ServiceBus;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace HospitalityOS.UnitTests.Adapters.Azure;

public sealed class ServiceBusSettingsTests
{
    private const string Emulator = "Endpoint=sb://localhost;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;";

    [Theory]
    [InlineData("Namespace", "events-dev-uksouth-sbns.servicebus.windows.net")]
    [InlineData("ConnectionString", Emulator)]
    public void Validate_OneWayToConnect_Passes(string key, string value)
    {
        IConfiguration settings = Settings((key, value));

        Assert.Empty(new AzureServiceBusEventTransportFactory().ValidateSettings(settings));
        Assert.Empty(new AzureServiceBusSchedulerFactory().ValidateSettings(settings));
    }

    [Fact]
    public void Validate_NoWayToConnect_Fails()
    {
        Assert.Contains(new AzureServiceBusEventTransportFactory().ValidateSettings(Settings()), problem => problem.Contains("exactly one of Namespace", StringComparison.Ordinal));
        Assert.Contains(new AzureServiceBusSchedulerFactory().ValidateSettings(Settings()), problem => problem.Contains("exactly one of Namespace", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_BothWaysToConnect_Fails()
    {
        IConfiguration settings = Settings(("Namespace", "events-dev-uksouth-sbns.servicebus.windows.net"), ("ConnectionString", Emulator));

        Assert.Contains(new AzureServiceBusEventTransportFactory().ValidateSettings(settings), problem => problem.Contains("exactly one of Namespace", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("0")]
    [InlineData("five")]
    public void Validate_BadMaxDeliveryCount_Fails(string count)
    {
        IConfiguration settings = Settings(("ConnectionString", Emulator), ("MaxDeliveryCount", count));

        Assert.Contains(new AzureServiceBusEventTransportFactory().ValidateSettings(settings), problem => problem.Contains("MaxDeliveryCount must be a positive integer", StringComparison.Ordinal));
    }

    private static IConfiguration Settings(params (string Key, string Value)[] values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values.Select(value => new KeyValuePair<string, string?>(value.Key, value.Value))).Build();
}
