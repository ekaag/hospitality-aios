using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace HospitalityOS.IntegrationTests.Hosts.Api;

/// <summary>
/// The demo clock the browser tests run on (L2): set, the API's time starts there and runs on; only in Development.
/// </summary>
public sealed class DemoClockTests
{
    private static readonly DateTimeOffset start = new(2026, 10, 26, 18, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Set_TheApisTimeStartsThere_AndRunsOn()
    {
        await using WebApplicationFactory<Program> factory = Host("Development", start.ToString("O"));
        TimeProvider clock = factory.Services.GetRequiredService<TimeProvider>();

        DateTimeOffset first = clock.GetUtcNow();
        await Task.Delay(50, TestContext.Current.CancellationToken);

        Assert.InRange(first, start, start.AddMinutes(1));
        Assert.True(clock.GetUtcNow() > first);
    }

    [Fact]
    public async Task NotSet_TheApiRunsOnTheSystemClock()
    {
        await using WebApplicationFactory<Program> factory = new();

        Assert.Same(TimeProvider.System, factory.Services.GetRequiredService<TimeProvider>());
    }

    [Theory]
    [InlineData("Production", "2026-10-26T18:00:00Z", "only Development may use")]
    [InlineData("Development", "tonight", "must be an ISO 8601 time")]
    public async Task OutsideDevelopment_OrUnreadable_StopsTheApiStarting(string environment, string value, string expected)
    {
        await using WebApplicationFactory<Program> factory = Host(environment, value);

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => factory.CreateClient());

        Assert.Contains(expected, exception.Message, StringComparison.Ordinal);
    }

    private static WebApplicationFactory<Program> Host(string environment, string value) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment(environment);
            builder.UseSetting("Demo:Clock:Start", value);
        });
}
