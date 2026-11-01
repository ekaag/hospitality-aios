using HospitalityOS.Ports.Persistence;

namespace HospitalityOS.Adapters.InMemory.Persistence;

/// <summary>
/// The in-memory store has no schema: there is nothing to migrate.
/// </summary>
internal sealed class InMemorySchemaMigrator : ISchemaMigrator
{
    public Task MigrateAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
