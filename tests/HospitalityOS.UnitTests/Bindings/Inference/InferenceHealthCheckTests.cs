using HospitalityOS.Bindings;
using HospitalityOS.Bindings.Inference;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Xunit;
using static HospitalityOS.UnitTests.Bindings.Inference.TestDoubles.InferenceTestHost;

namespace HospitalityOS.UnitTests.Bindings.Inference;

public sealed class InferenceHealthCheckTests
{
    private static readonly string[] toSecondary = ["secondary:backup-model"];

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Check_ProbesEveryProviderEachTaskMayUse_ForThePortItNeeds()
    {
        HealthCheckResult inference = await CheckAsync(Settings(
            Provider("primary"),
            Provider("secondary"),
            Provider("unused", "Test", ("Healthy", "false")),
            Route(Answer, "primary", "big-model", toSecondary),
            Route(Classify, "primary", "small-model")));

        Assert.Equal(HealthStatus.Healthy, inference.Status);
        Assert.Equal(["primary/IChatModel", "primary/IClassifier", "secondary/IChatModel"], inference.Data.Keys.Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task Check_AnUnhealthyFallback_OrAProviderThatCannotBeCreated_IsUnhealthy()
    {
        HealthCheckResult inference = await CheckAsync(Settings(
            Provider("primary", "Test", ("Behavior", "uncreatable")),
            Provider("secondary", "Test", ("Healthy", "false")),
            Route(Answer, "secondary", "big-model"),
            Route(Classify, "primary", "small-model")));

        Assert.Equal(HealthStatus.Unhealthy, inference.Status);
        Assert.Equal("Cannot reach the provider.", inference.Data["primary/IClassifier"]);
        Assert.Equal("secondary is down", inference.Data["secondary/IChatModel"]);
    }

    private static async Task<HealthCheckResult> CheckAsync(Dictionary<string, string?> settings)
    {
        using ServiceProvider services = Build(settings);
        InferenceHealthCheck check = new(services.GetRequiredService<InferenceCatalog>());
        HealthCheckContext context = new()
        {
            Registration = new HealthCheckRegistration(DependencyInjection.InferenceHealthCheckName, check, HealthStatus.Unhealthy, tags: null),
        };

        return await check.CheckHealthAsync(context, Cancellation);
    }
}
