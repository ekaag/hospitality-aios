using HospitalityOS.Adapters.EfCore.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace HospitalityOS.Adapters.EfCore.Sqlite.Persistence;

/// <summary>
/// SQLite cannot compare or sort <see cref="DateTimeOffset"/> values, which the repositories need (newest first), so
/// they are stored as UTC ticks. Timestamps are UTC throughout (coding standards), so no information is lost.
/// </summary>
internal sealed class SqliteModelConventions : IProviderModelConventions
{
    public void Configure(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<DateTimeOffset>().HaveConversion<UtcTicksConverter>();
    }

    private sealed class UtcTicksConverter() : ValueConverter<DateTimeOffset, long>(
        value => value.UtcTicks,
        ticks => new DateTimeOffset(ticks, TimeSpan.Zero));
}
