using HospitalityOS.Bindings;
using HospitalityOS.UnitTests.Bindings.TestDoubles;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Xunit;

namespace HospitalityOS.UnitTests.Bindings;

public sealed class BindingsHealthCheckTests
{
    [Fact]
    public async Task CheckHealthAsync_WhenAllBindingsHealthy_ReturnsHealthy()
    {
        using ServiceProvider provider = BindingTestHost.Build(
            new Dictionary<string, string?>
            {
                ["Bindings:Greeter:Default:Adapter"] = "English",
                ["Bindings:Greeter:Operations:vip-welcome:Adapter"] = "French",
            },
            GreeterFactory.English(),
            GreeterFactory.French());

        HealthCheckResult result = await CheckHealthAsync(provider);

        Assert.Equal(HealthStatus.Healthy, result.Status);
        Assert.Equal(2, result.Data.Count);
    }

    [Fact]
    public async Task CheckHealthAsync_WhenAdapterProbeFails_ReturnsUnhealthyWithFailingBinding()
    {
        using ServiceProvider provider = BindingTestHost.Build(
            new Dictionary<string, string?>
            {
                ["Bindings:Greeter:Default:Adapter"] = "English",
                ["Bindings:Greeter:Operations:vip-welcome:Adapter"] = "French",
                ["Bindings:Greeter:Operations:vip-welcome:Settings:Healthy"] = "false",
            },
            GreeterFactory.English(),
            GreeterFactory.French());

        HealthCheckResult result = await CheckHealthAsync(provider);

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.Contains("Bindings:Greeter:Operations:vip-welcome", result.Description, StringComparison.Ordinal);
        Assert.Equal("French: Bonjour greeter is down", result.Data["Bindings:Greeter:Operations:vip-welcome"]);
    }

    [Fact]
    public async Task CheckHealthAsync_WhenBindingSelectsUnknownAdapter_ReturnsUnhealthy()
    {
        using ServiceProvider provider = BindingTestHost.Build(
            new Dictionary<string, string?> { ["Bindings:Greeter:Default:Adapter"] = "Klingon" },
            GreeterFactory.English());

        HealthCheckResult result = await CheckHealthAsync(provider);

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
    }

    private static Task<HealthCheckResult> CheckHealthAsync(ServiceProvider provider)
    {
        BindingsHealthCheck check = new(provider, provider.GetServices<PortRegistration>());
        HealthCheckContext context = new()
        {
            Registration = new HealthCheckRegistration(DependencyInjection.HealthCheckName, check, HealthStatus.Unhealthy, tags: null),
        };

        return check.CheckHealthAsync(context, TestContext.Current.CancellationToken);
    }
}
