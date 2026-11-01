using HospitalityOS.Adapters.EfCore.Persistence;
using HospitalityOS.Ports.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
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
    public void Start_WithPostgreSqlProvider_UsesEfCoreOnNpgsql()
    {
        // Resolving the unit of work opens no connection, so no server is needed.
        using WebApplicationFactory<Program> factory = WithSettings(("Persistence:Provider", "PostgreSql"), ("Persistence:ConnectionString", "Host=localhost;Database=hospitalityos"));
        using IServiceScope scope = factory.Services.CreateScope();

        Assert.Equal("EfUnitOfWork", scope.ServiceProvider.GetRequiredService<IUnitOfWork>().GetType().Name);
        Assert.Equal("Npgsql.EntityFrameworkCore.PostgreSQL", scope.ServiceProvider.GetRequiredService<HospitalityDbContext>().Database.ProviderName);
    }

    [Fact]
    public void Start_WithPostgreSqlSigningInAsEntra_ConnectsThroughATokenDataSource_WithoutConnecting()
    {
        using WebApplicationFactory<Program> factory = WithSettings(
            ("Persistence:Provider", "PostgreSql"),
            ("Persistence:SignIn", "Entra"),
            ("Persistence:ConnectionString", "Host=data-dev-uksouth-psql.postgres.database.azure.com;Database=hospitalityos;Username=identity-api-dev-uksouth-id;Ssl Mode=Require"));
        using IServiceScope scope = factory.Services.CreateScope();

        Assert.Equal("EfUnitOfWork", scope.ServiceProvider.GetRequiredService<IUnitOfWork>().GetType().Name);
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<NpgsqlDataSource>());
        Assert.Equal("EfSchemaMigrator", scope.ServiceProvider.GetRequiredService<ISchemaMigrator>().GetType().Name);
    }

    [Theory]
    [InlineData("Persistence:SignIn", "Kerberos", "Persistence:SignIn 'Kerberos' is unknown")]
    [InlineData("Persistence:AppRole:Name", "identity-api-dev-uksouth-id", "Persistence:AppRole:ObjectId must be the role's Entra object ID")]
    [InlineData("Persistence:AppRole:Name", "app\"; DROP TABLE x; --", "is not a role or database name")]
    public void Start_WithPostgreSqlSignInOrAppRoleWrong_FailsBeforeServingRequests(string key, string value, string expected)
    {
        using WebApplicationFactory<Program> factory = WithSettings(
            ("Persistence:Provider", "PostgreSql"),
            ("Persistence:ConnectionString", "Host=localhost;Database=hospitalityos"),
            ("Persistence:AppRole:ObjectId", key == "Persistence:AppRole:Name" && value.Contains("DROP", StringComparison.Ordinal) ? Guid.NewGuid().ToString() : ""),
            (key, value));

        Exception exception = Assert.ThrowsAny<Exception>(() => factory.CreateClient());

        Assert.Contains(expected, exception.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Start_WithInMemoryPersistence_HasNothingToMigrate()
    {
        using WebApplicationFactory<Program> factory = new();
        using IServiceScope scope = factory.Services.CreateScope();

        Assert.Equal("InMemorySchemaMigrator", scope.ServiceProvider.GetRequiredService<ISchemaMigrator>().GetType().Name);
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

    [Fact]
    public void Start_WithPostgreSqlAndNoConnectionString_FailsBeforeServingRequests()
    {
        using WebApplicationFactory<Program> factory = WithSettings(("Persistence:Provider", "PostgreSql"));

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => factory.CreateClient());

        Assert.Contains("Persistence:ConnectionString is required for the PostgreSql provider", exception.Message, StringComparison.Ordinal);
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
