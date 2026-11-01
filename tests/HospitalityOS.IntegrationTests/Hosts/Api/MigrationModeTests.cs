using System.Diagnostics;
using Npgsql;
using Xunit;

namespace HospitalityOS.IntegrationTests.Hosts.Api;

/// <summary>
/// The deployment's migration step as it runs in a job: the API executable in <c>migrate</c> mode, with the deployed
/// settings, against a real PostgreSQL server (<c>HOSPITALITYOS_POSTGRES</c>; skipped without it, required in CI), and on
/// SQLite everywhere. It must make the schema, let the app's role read and write it, seed, and exit; and running it again
/// must change nothing.
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

    [Fact]
    public async Task Migrate_OnSqlite_MakesTheSchemaAndSeedsOnce_AndExits()
    {
        string file = Path.Combine(Path.GetTempPath(), $"hospitalityos-migrate-{Guid.NewGuid():N}.db");
        Dictionary<string, string> settings = new()
        {
            ["Persistence__Provider"] = "Sqlite",
            ["Persistence__ConnectionString"] = $"Data Source={file};Pooling=False",
            ["Seed__AuroraGrand__Enabled"] = "true",
        };

        try
        {
            (int first, string firstOutput) = await RunAsync(settings);
            (int second, string secondOutput) = await RunAsync(settings);

            Assert.True(first == 0, firstOutput);
            Assert.True(second == 0, secondOutput);
            Assert.Matches(@"seeded in tenant [0-9a-f-]+: [1-9]\d* records created, 0 already present", firstOutput);
            Assert.Matches(@"seeded in tenant [0-9a-f-]+: 0 records created, [1-9]\d* already present", secondOutput);
        }
        finally
        {
            File.Delete(file);
        }
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

    private Task<(int ExitCode, string Output)> MigrateAsync() =>
        RunAsync(new Dictionary<string, string>
        {
            ["Persistence__Provider"] = "PostgreSql",
            ["Persistence__ConnectionString"] = Database(),
            ["Persistence__AppRole__Name"] = role,
            ["Persistence__AppRole__ObjectId"] = Guid.NewGuid().ToString(),
            ["Seed__AuroraGrand__Enabled"] = "true",
        });

    /// <summary>
    /// The API executable in migrate mode with these settings; one that has not exited within two minutes is stopped and
    /// fails, as the migration job would.
    /// </summary>
    private static async Task<(int ExitCode, string Output)> RunAsync(Dictionary<string, string> settings)
    {
        ProcessStartInfo start = new("dotnet", ["HospitalityOS.Api.dll", "migrate"])
        {
            WorkingDirectory = AppContext.BaseDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        start.Environment["ASPNETCORE_ENVIRONMENT"] = "Production";
        start.Environment["ASPNETCORE_URLS"] = "http://127.0.0.1:0";

        foreach ((string key, string value) in settings)
        {
            start.Environment[key] = value;
        }

        using Process process = Process.Start(start)!;
        Task<string> output = process.StandardOutput.ReadToEndAsync(Cancellation);
        Task<string> errors = process.StandardError.ReadToEndAsync(Cancellation);
        using CancellationTokenSource limit = CancellationTokenSource.CreateLinkedTokenSource(Cancellation);
        limit.CancelAfter(TimeSpan.FromMinutes(2));

        try
        {
            await process.WaitForExitAsync(limit.Token);
        }
        catch (OperationCanceledException) when (!Cancellation.IsCancellationRequested)
        {
            process.Kill(entireProcessTree: true);

            return (-1, "The migrate mode did not exit within two minutes.");
        }

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
