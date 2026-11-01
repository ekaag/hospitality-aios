using HospitalityOS.Adapters.EfCore.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace HospitalityOS.Adapters.EfCore.PostgreSql.Persistence;

/// <summary>
/// PostgreSQL reports unique and primary key violations as SQLSTATE 23505 with the table and the constraint: EF Core names
/// a unique index <c>IX_{Table}_{Column}_..._{Column}</c>, so its last column follows the last underscore (column names
/// have none); a primary key, <c>PK_{Table}</c>, is the ID.
/// </summary>
internal sealed class PostgreSqlErrorClassifier : IDatabaseErrorClassifier
{
    public bool IsUniqueViolation(DbUpdateException exception, out string? table, out string? column)
    {
        table = null;
        column = null;

        if (exception.InnerException is not PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } postgres)
        {
            return false;
        }

        table = postgres.TableName;
        column = postgres.ConstraintName switch
        {
            { } name when name.StartsWith("PK_", StringComparison.Ordinal) => "Id",
            { } name when name.StartsWith("IX_", StringComparison.Ordinal) && name.LastIndexOf('_') is var last and > 2 => name[(last + 1)..],
            _ => null,
        };

        return true;
    }
}
