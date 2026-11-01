using HospitalityOS.Adapters.EfCore.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HospitalityOS.Adapters.EfCore.PostgreSql.Persistence;

/// <summary>
/// <see cref="DateTimeOffset"/> values are UTC throughout (coding standards) and map to <c>timestamp with time zone</c>,
/// which Npgsql writes only at offset zero. <see cref="DateTime"/> values are property-local wall-clock times (an event's
/// start, a request's local time), so they are stored as written, <c>timestamp without time zone</c>, rather than Npgsql's
/// default, which would refuse them as not UTC.
/// </summary>
internal sealed class PostgreSqlModelConventions : IProviderModelConventions
{
    public void Configure(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<DateTime>().HaveColumnType("timestamp without time zone");
        configurationBuilder.Properties<DateTime?>().HaveColumnType("timestamp without time zone");
    }
}
