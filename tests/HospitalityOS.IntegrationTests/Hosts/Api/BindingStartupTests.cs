using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Azure.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace HospitalityOS.IntegrationTests.Hosts.Api;

public sealed class BindingStartupTests
{
    [Fact]
    public void Start_WithBindingsForUnknownPort_FailsBeforeServingRequests()
    {
        using WebApplicationFactory<Program> factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder => builder.UseSetting("Bindings:NoSuchPort:Default:Adapter", "InMemory"));

        OptionsValidationException exception = Assert.Throws<OptionsValidationException>(() => factory.CreateClient());

        Assert.Contains(exception.Failures, failure => failure.Contains("'Bindings:NoSuchPort' configures an unknown port", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("EventTransport", "AzureServiceBus", "exactly one of Namespace")]
    [InlineData("Scheduler", "AzureServiceBus", "exactly one of Namespace")]
    [InlineData("BlobStore", "AzureBlobStorage", "exactly one of ServiceUri")]
    [InlineData("BlobStore", "FileSystem", "Root must name the folder")]
    [InlineData("SecretStore", "AzureKeyVault", "VaultUri must be the vault's https URL")]
    [InlineData("SigningKey", "AzureKeyVault", "VaultUri must be the vault's https URL")]
    [InlineData("SearchIndex", "AzureAISearch", "Endpoint must be the search service's https URL")]
    [InlineData("VectorStore", "AzureAISearch", "Endpoint must be the search service's https URL")]
    [InlineData("RealtimeNotifier", "AzureSignalR", "Endpoint must be the Azure SignalR Service's https URL")]
    public void Start_WithAnAdapterThatCannotConnect_FailsBeforeServingRequests(string port, string adapter, string expected)
    {
        using WebApplicationFactory<Program> factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder => builder.UseSetting($"Bindings:{port}:Default:Adapter", adapter));

        OptionsValidationException exception = Assert.Throws<OptionsValidationException>(() => factory.CreateClient());

        Assert.Contains(exception.Failures, failure => failure.Contains(expected, StringComparison.Ordinal));
    }

    [Fact]
    public void Start_WithAzureSignalRBound_CarriesConnectionsThroughTheService()
    {
        using WebApplicationFactory<Program> factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Bindings:RealtimeNotifier:Default:Adapter", "AzureSignalR");
            builder.UseSetting("Bindings:RealtimeNotifier:Default:Settings:Endpoint", "https://realtime-dev-uksouth-sigr.service.signalr.net");
        });

        ServiceEndpoint endpoint = Assert.Single(factory.Services.GetRequiredService<IOptions<ServiceOptions>>().Value.Endpoints ?? []);

        Assert.Equal("https://realtime-dev-uksouth-sigr.service.signalr.net", endpoint.Endpoint);
    }

    [Fact]
    public void Start_WithInProcessSignalR_UsesNoService()
    {
        using WebApplicationFactory<Program> factory = new();

        Assert.Empty(factory.Services.GetRequiredService<IOptions<ServiceOptions>>().Value.Endpoints ?? []);
    }
}
