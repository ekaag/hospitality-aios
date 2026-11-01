using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace HospitalityOS.Adapters.EfCore.PostgreSql.Persistence;

/// <summary>
/// The one place PostgreSQL is configured, shared by the running app and the design-time factory, so migrations are
/// generated against exactly what runs.
/// </summary>
internal static class PostgreSqlDbContextOptions
{
    public static void Configure(DbContextOptionsBuilder options, string connectionString)
    {
        options.UseNpgsql(connectionString, npgsql => npgsql.MigrationsAssembly(MigrationsAssembly));
    }

    public static void Configure(DbContextOptionsBuilder options, NpgsqlDataSource dataSource)
    {
        options.UseNpgsql(dataSource, npgsql => npgsql.MigrationsAssembly(MigrationsAssembly));
    }

    private static string? MigrationsAssembly => typeof(PostgreSqlDbContextOptions).Assembly.GetName().Name;
}
