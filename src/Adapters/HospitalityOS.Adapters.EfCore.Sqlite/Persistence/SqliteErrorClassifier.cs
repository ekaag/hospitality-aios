using System.Text.RegularExpressions;
using HospitalityOS.Adapters.EfCore.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace HospitalityOS.Adapters.EfCore.Sqlite.Persistence;

/// <summary>
/// SQLite reports unique and primary key violations as constraint errors with extended codes 2067 and 1555, and
/// names the columns in the message: <c>UNIQUE constraint failed: AccommodationUnits.PropertyId, AccommodationUnits.UnitNumberKey</c>.
/// </summary>
internal sealed partial class SqliteErrorClassifier : IDatabaseErrorClassifier
{
    private const int UniqueConstraint = 2067;
    private const int PrimaryKeyConstraint = 1555;

    public bool IsUniqueViolation(DbUpdateException exception, out string? table, out string? column)
    {
        table = null;
        column = null;

        if (exception.InnerException is not SqliteException { SqliteExtendedErrorCode: UniqueConstraint or PrimaryKeyConstraint } sqlite)
        {
            return false;
        }

        Match match = LastConstraintColumn().Match(sqlite.Message);

        if (match.Success)
        {
            table = match.Groups["table"].Value;
            column = match.Groups["column"].Value;
        }

        return true;
    }

    [GeneratedRegex(@"constraint failed: (?:\w+\.\w+, )*(?<table>\w+)\.(?<column>\w+)")]
    private static partial Regex LastConstraintColumn();
}
