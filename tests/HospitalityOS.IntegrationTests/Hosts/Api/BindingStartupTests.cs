using Microsoft.AspNetCore.Mvc.Testing;
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
}
