using HospitalityOS.Adapters.EfCore.Persistence;
using HospitalityOS.Ports.Tenancy;
using HospitalityOS.SharedKernel.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace HospitalityOS.Adapters.EfCore.Sqlite.Persistence;

/// <summary>
/// Lets <c>dotnet ef migrations add</c> build the model without a host. Nothing is queried at design time, so the
/// tenant is a placeholder.
/// </summary>
internal sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<HospitalityDbContext>
{
    public HospitalityDbContext CreateDbContext(string[] args)
    {
        DbContextOptionsBuilder<HospitalityDbContext> options = new();
        SqliteDbContextOptions.Configure(options, "Data Source=design-time.db");

        return new HospitalityDbContext(options.Options, new DesignTimeTenant(), [new SqliteModelConventions()]);
    }

    private sealed class DesignTimeTenant : ICurrentTenant
    {
        public TenantId TenantId => throw new InvalidOperationException("No tenant at design time.");
    }
}
