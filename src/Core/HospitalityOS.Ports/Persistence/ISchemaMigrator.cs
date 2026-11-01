namespace HospitalityOS.Ports.Persistence;

/// <summary>
/// Brings the store's schema up to date and lets the app use it: run by the deployment's migration step (the API's
/// <c>migrate</c> mode), never by the app while it starts. Idempotent: on an up-to-date store it changes nothing.
/// </summary>
public interface ISchemaMigrator
{
    /// <summary>
    /// Applies every pending migration, then grants the app's own sign-in what it needs, where the store has one.
    /// </summary>
    /// <param name="cancellationToken">Cancels the migration; a migration already applying finishes or rolls back.</param>
    Task MigrateAsync(CancellationToken cancellationToken);
}
