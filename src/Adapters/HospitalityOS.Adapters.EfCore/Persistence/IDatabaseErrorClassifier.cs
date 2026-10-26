using Microsoft.EntityFrameworkCore;

namespace HospitalityOS.Adapters.EfCore.Persistence;

/// <summary>
/// Recognizes the provider's unique-constraint violation, which every database reports differently, so the unit of
/// work can return a duplicate-key conflict instead of an exception. Registered by the provider project.
/// </summary>
internal interface IDatabaseErrorClassifier
{
    /// <summary>
    /// Whether <paramref name="exception"/> is a unique or primary key violation.
    /// </summary>
    /// <param name="exception">The failed save.</param>
    /// <param name="table">The table, when the provider reports it.</param>
    /// <param name="column">The last column of the violated key, when the provider reports it.</param>
    bool IsUniqueViolation(DbUpdateException exception, out string? table, out string? column);
}
