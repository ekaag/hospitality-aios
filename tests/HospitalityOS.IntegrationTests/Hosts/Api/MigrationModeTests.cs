using System.Diagnostics;
using Npgsql;
using Xunit;

namespace HospitalityOS.IntegrationTests.Hosts.Api;

/// <summary>
/// The deployment's migration step as it runs in a job: the API executable in <c>migrate</c> mode, with the deployed
/// settings, against a real PostgreSQL server (<c>HOSPITALITYOS_POSTGRES</c>; skipped without it, required in CI). It
/// must make the schema, let the app's role read and write it, seed, and exit; and running it again must change nothing.
/// </summary>
public sealed class MigrationModeTests : IAsyncLifetime
{
    private const string ServerVariable = "HOSPITALITYOS_POSTGRES";
    private const string RequiredVariable = "HOSPITALITYOS_POSTGRES_REQUIRED";

    private readonly string? server = Environment.GetEnvironmentVariable(ServerVariable);
    private readonly string database = $"migrate_{Guid.NewGuid():N}";
    private readonly string role = $"app_{Guid.NewGuid():N}";
    private bool created;

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    private static bool Required => string.Equals(Environment.GetEnvironmentVariable(RequiredVariable), "true", StringComparison.OrdinalIgnoreCase);

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task Migrate_MakesTheSchema_GrantsTheAppRole_AndSeeds_AndAgainChangesNothing()
    {
        Assert.SkipWhen(string.IsNullOrWhiteSpace(server) && !Required, $"Set {ServerVariable} to a PostgreSQL server to run the migration step's tests.");
        Assert.False(string.IsNullOrWhiteSpace(server), $"{RequiredVariable} is set, so {ServerVariable} must name a PostgreSQL server.");

        await ExecuteAsync(server!, $"CREATE DATABASE \"{database}\"");
        created = true;
        await ExecuteAsync(server!, $"CREATE ROLE \"{role}\" NOLOGIN");

        (int first, string firstOutput) = await MigrateAsync();
        Assert.True(first == 0, firstOutput);
        long organizations = await CountAsRoleAsync("Organizations");
        long contentItems = await CountAsRoleAsync("ContentItems");

        (int second, string secondOutput) = await MigrateAsync();
        Assert.True(second == 0, secondOutput);

        Assert.True(organizations > 0, "The seed made no organization.");
        Assert.True(contentItems > 0, "The seed made no content.");
        Assert.Equal((organizations, contentItems), (await CountAsRoleAsync("Organizations"), await CountAsRoleAsync("ContentItems")));
        await Assert.ThrowsAsync<PostgresException>(() => ExecuteAsync(Database(), $"SET ROLE \"{role}\"; CREATE TABLE \"Intruder\" (\"Id\" integer)"));
    }

    public async ValueTask DisposeAsync()
    {
        if (!created)
        {
            return;
        }

        NpgsqlConnection.ClearAllPools();
        await ExecuteAsync(server!, $"DROP DATABASE IF EXISTS \"{database}\" WITH (FORCE)");
        await ExecuteAsync(server!, $"DROP ROLE IF EXISTS \"{role}\"");
    }

    private string Database() => new NpgsqlConnectionStringBuilder(server) { Database = database }.ConnectionString;

    private async Task<(int ExitCode, string Output)> MigrateAsync()
    {
        ProcessStartInfo start = new("dotnet", ["HospitalityOS.Api.dll", "migrate"])
        {
            WorkingDirectory = AppContext.BaseDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        start.Environment["ASPNETCORE_ENVIRONMENT"] = "Production";
        start.Environment["Persistence__Provider"] = "PostgreSql";
        start.Environment["Persistence__ConnectionString"] = Database();
        start.Environment["Persistence__AppRole__Name"] = role;
        start.Environment["Persistence__AppRole__ObjectId"] = Guid.NewGuid().ToString();
        start.Environment["Seed__AuroraGrand__Enabled"] = "true";

        using Process process = Process.Start(start)!;
        Task<string> output = process.StandardOutput.ReadToEndAsync(Cancellation);
        Task<string> errors = process.StandardError.ReadToEndAsync(Cancellation);
        await process.WaitForExitAsync(Cancellation);

        return (process.ExitCode, await output + await errors);
    }

    private async Task<long> CountAsRoleAsync(string table)
    {
        await using NpgsqlConnection connection = new(Database());
        await connection.OpenAsync(Cancellation);
        await using NpgsqlCommand count = new($"SET ROLE \"{role}\"; SELECT count(*) FROM \"{table}\"", connection);

        return (long)(await count.ExecuteScalarAsync(Cancellation))!;
    }

    private static async Task ExecuteAsync(string connectionString, string sql)
    {
        await using NpgsqlConnection connection = new(connectionString);
        await connection.OpenAsync();
        await using NpgsqlCommand command = new(sql, connection);
        await command.ExecuteNonQueryAsync();
    }
}
