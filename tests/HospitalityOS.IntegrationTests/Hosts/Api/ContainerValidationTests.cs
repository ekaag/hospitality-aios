using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace HospitalityOS.IntegrationTests.Hosts.Api;

/// <summary>
/// P15: the host validates its container in every environment, so wiring errors stop startup.
/// </summary>
public sealed class ContainerValidationTests
{
    [Fact]
    public async Task Start_InProduction_ServesHealth()
    {
        using WebApplicationFactory<Program> factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder => builder.UseEnvironment("Production"));
        using HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync(new Uri("/health", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public void Start_InProduction_WithSingletonCapturingScopedService_Fails()
    {
        using WebApplicationFactory<Program> factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder => builder
                .UseEnvironment("Production")
                .ConfigureServices(services =>
                {
                    services.AddScoped<ScopedDependency>();
                    services.AddSingleton<SingletonCapturingScoped>();
                }));

        AggregateException exception = Assert.Throws<AggregateException>(() => factory.CreateClient());

        Assert.Contains(exception.InnerExceptions, inner => inner.Message.Contains(nameof(ScopedDependency), StringComparison.Ordinal));
    }

    private sealed class ScopedDependency;

    private sealed class SingletonCapturingScoped(ScopedDependency dependency)
    {
        public ScopedDependency Dependency { get; } = dependency;
    }
}
