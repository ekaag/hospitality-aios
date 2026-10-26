using HospitalityOS.Adapters.EfCore.Persistence;
using HospitalityOS.Adapters.EfCore.Sqlite.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace HospitalityOS.Adapters.EfCore.Sqlite;

/// <summary>
/// Registers EF Core persistence on SQLite: the local and test database. The schema comes from this project's
/// migrations, applied by the deployment (or <c>dotnet ef database update</c> locally), never by the app itself.
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Registers the unit of work and every repository over a SQLite database. The host must also register <c>ICurrentTenant</c>.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="connectionString">A SQLite connection string, e.g. <c>Data Source=hospitality.db</c>.</param>
    public static IServiceCollection AddSqlitePersistence(this IServiceCollection services, string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        services.AddDbContext<HospitalityDbContext>(options => SqliteDbContextOptions.Configure(options, connectionString));
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IProviderModelConventions, SqliteModelConventions>());
        services.TryAddSingleton<IDatabaseErrorClassifier, SqliteErrorClassifier>();

        return services.AddEfCorePersistence();
    }
}
