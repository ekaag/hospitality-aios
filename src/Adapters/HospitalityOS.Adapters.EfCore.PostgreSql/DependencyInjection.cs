using HospitalityOS.Adapters.EfCore.Persistence;
using HospitalityOS.Adapters.EfCore.PostgreSql.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace HospitalityOS.Adapters.EfCore.PostgreSql;

/// <summary>
/// Registers EF Core persistence on PostgreSQL (K1): the production database, offered as a managed service by every
/// cloud (Azure Database for PostgreSQL, AWS RDS, GCP Cloud SQL), so a deployment plan can place it on any of them. The
/// schema comes from this project's migrations, applied by the deployment (or <c>dotnet ef database update</c> locally),
/// never by the app itself.
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Registers the unit of work and every repository over a PostgreSQL database. The host must also register
    /// <c>ICurrentTenant</c>.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="connectionString">An Npgsql connection string, e.g.
    /// <c>Host=localhost;Database=hospitalityos;Username=app;Password=...</c>.</param>
    public static IServiceCollection AddPostgreSqlPersistence(this IServiceCollection services, string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        services.AddDbContext<HospitalityDbContext>(options => PostgreSqlDbContextOptions.Configure(options, connectionString));
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IProviderModelConventions, PostgreSqlModelConventions>());
        services.TryAddSingleton<IDatabaseErrorClassifier, PostgreSqlErrorClassifier>();

        return services.AddEfCorePersistence();
    }
}
