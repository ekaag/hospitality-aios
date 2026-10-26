using Microsoft.EntityFrameworkCore;

namespace HospitalityOS.Adapters.EfCore.Sqlite.Persistence;

/// <summary>
/// The one place SQLite is configured, shared by the running app and the design-time factory, so migrations are
/// generated against exactly what runs.
/// </summary>
internal static class SqliteDbContextOptions
{
    public static void Configure(DbContextOptionsBuilder options, string connectionString)
    {
        options.UseSqlite(connectionString, sqlite => sqlite.MigrationsAssembly(typeof(SqliteDbContextOptions).Assembly.GetName().Name));
    }
}
