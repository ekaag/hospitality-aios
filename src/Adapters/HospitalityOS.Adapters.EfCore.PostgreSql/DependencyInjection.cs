using HospitalityOS.Adapters.EfCore.Persistence;
using HospitalityOS.Adapters.EfCore.PostgreSql.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;

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
    /// Registers the unit of work and every repository over a PostgreSQL database, signing in with the connection
    /// string's password. The host must also register <c>ICurrentTenant</c>.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="connectionString">An Npgsql connection string, e.g.
    /// <c>Host=localhost;Database=hospitalityos;Username=app;Password=...</c>.</param>
    public static IServiceCollection AddPostgreSqlPersistence(this IServiceCollection services, string connectionString) =>
        services.AddPostgreSqlPersistence(new PostgreSqlOptions { ConnectionString = connectionString });

    /// <summary>
    /// Registers the unit of work and every repository over a PostgreSQL database, as <paramref name="options"/> says to
    /// reach it. The host must also register <c>ICurrentTenant</c>.
    /// </summary>
    public static IServiceCollection AddPostgreSqlPersistence(this IServiceCollection services, PostgreSqlOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.ConnectionString);

        string connectionString = options.ConnectionString;

        if (options.EntraSignIn)
        {
            services.TryAddSingleton(_ => PostgreSqlDataSources.Entra(connectionString));
            services.AddDbContext<HospitalityDbContext>((provider, builder) => PostgreSqlDbContextOptions.Configure(builder, provider.GetRequiredService<NpgsqlDataSource>()));
        }
        else
        {
            services.AddDbContext<HospitalityDbContext>(builder => PostgreSqlDbContextOptions.Configure(builder, connectionString));
        }

        services.TryAddEnumerable(ServiceDescriptor.Singleton<IProviderModelConventions, PostgreSqlModelConventions>());
        services.TryAddSingleton<IDatabaseErrorClassifier, PostgreSqlErrorClassifier>();

        if (options.AppRoleName is { } role)
        {
            PostgreSqlAppRoleAccess.Quote(role);
            services.TryAddSingleton(new PostgreSqlConnection(connectionString, options.EntraSignIn, new PostgreSqlAppRole(role, options.AppRoleObjectId)));
            services.TryAddEnumerable(ServiceDescriptor.Scoped<ISchemaAccess, PostgreSqlAppRoleAccess>());
        }

        return services.AddEfCorePersistence();
    }
}
