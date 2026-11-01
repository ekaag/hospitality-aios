using HospitalityOS.Api.Seeding;
using HospitalityOS.Ports.Persistence;

namespace HospitalityOS.Api.Persistence;

/// <summary>
/// The API's <c>migrate</c> mode, the deployment's migration step (run as a job before a new API revision starts): with
/// the API's own configuration, it applies the schema (<see cref="ISchemaMigrator"/>), then seeds the Aurora Grand when
/// <c>Seed:AuroraGrand:Enabled</c>, and exits. It serves nothing and starts none of the API's background work, so events
/// the seed raises wait in the outbox for the API to publish. Every step is idempotent, so running it again on the same
/// release changes nothing; a failure exits non-zero and stops the deployment.
/// </summary>
internal static partial class MigrationMode
{
    public const string Argument = "migrate";

    public static bool Requested(string[] args) => args is [Argument, ..];

    public static async Task RunAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        ILogger logger = services.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(MigrationMode));

        await using (AsyncServiceScope scope = services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<ISchemaMigrator>().MigrateAsync(cancellationToken).ConfigureAwait(false);
        }

        LogMigrated(logger);

        if (services.GetServices<IHostedService>().OfType<ReferenceDataSeeding>().SingleOrDefault() is { } seeding)
        {
            await seeding.StartAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "The schema is up to date")]
    private static partial void LogMigrated(ILogger logger);
}
