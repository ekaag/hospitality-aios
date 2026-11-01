using HospitalityOS.Adapters.EfCore.Persistence;
using HospitalityOS.Ports.Tenancy;
using HospitalityOS.SharedKernel.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace HospitalityOS.Adapters.EfCore.PostgreSql.Persistence;

/// <summary>
/// Lets <c>dotnet ef migrations add</c> build the model without a host or a database. Nothing is queried at design time,
/// so the tenant is a placeholder and the connection string names no real server.
/// </summary>
internal sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<HospitalityDbContext>
{
    public HospitalityDbContext CreateDbContext(string[] args)
    {
        DbContextOptionsBuilder<HospitalityDbContext> options = new();
        PostgreSqlDbContextOptions.Configure(options, "Host=design-time;Database=hospitalityos");

        return new HospitalityDbContext(options.Options, new DesignTimeTenant(), [new PostgreSqlModelConventions()]);
    }

    private sealed class DesignTimeTenant : ICurrentTenant
    {
        public TenantId TenantId => throw new InvalidOperationException("No tenant at design time.");
    }
}
