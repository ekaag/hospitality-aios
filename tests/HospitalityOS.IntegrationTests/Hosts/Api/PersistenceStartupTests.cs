using HospitalityOS.Ports.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace HospitalityOS.IntegrationTests.Hosts.Api;

public sealed class PersistenceStartupTests
{
    [Fact]
    public void Start_WithDefaultConfiguration_UsesInMemoryPersistence()
    {
        using WebApplicationFactory<Program> factory = new();

        Assert.Equal("InMemoryUnitOfWork", ResolveUnitOfWork(factory).GetType().Name);
    }

    [Fact]
    public void Start_WithSqliteProvider_UsesEfCorePersistence()
    {
        using WebApplicationFactory<Program> factory = WithSettings(("Persistence:Provider", "Sqlite"), ("Persistence:ConnectionString", "Data Source=:memory:"));

        Assert.Equal("EfUnitOfWork", ResolveUnitOfWork(factory).GetType().Name);
    }

    [Fact]
    public void Start_WithUnknownProvider_FailsBeforeServingRequests()
    {
        using WebApplicationFactory<Program> factory = WithSettings(("Persistence:Provider", "Cassandra"));

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => factory.CreateClient());

        Assert.Contains("Persistence:Provider 'Cassandra' is unknown", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Start_WithSqliteAndNoConnectionString_FailsBeforeServingRequests()
    {
        using WebApplicationFactory<Program> factory = WithSettings(("Persistence:Provider", "Sqlite"));

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => factory.CreateClient());

        Assert.Contains("Persistence:ConnectionString is required", exception.Message, StringComparison.Ordinal);
    }

    private static WebApplicationFactory<Program> WithSettings(params (string Key, string Value)[] settings)
    {
        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            foreach ((string key, string value) in settings)
            {
                builder.UseSetting(key, value);
            }
        });
    }

    private static IUnitOfWork ResolveUnitOfWork(WebApplicationFactory<Program> factory)
    {
        using IServiceScope scope = factory.Services.CreateScope();

        return scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
    }
}
