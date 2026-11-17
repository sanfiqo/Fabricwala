using Microsoft.EntityFrameworkCore;
using MySql.Data.MySqlClient;
using TextileConnect.Infrastructure.Persistence;

namespace TextileConnect.Tests;

/// <summary>A test that needs a real MySQL server. It is reported as skipped (not as passed) when TEXTILECONNECT_TEST_MYSQL is not set.</summary>
public sealed class MySqlFactAttribute : FactAttribute
{
    public MySqlFactAttribute()
    {
        if (!MySqlStore.IsConfigured) Skip = $"Set {MySqlStore.Variable} to a MySQL server connection string to run this test (see docs/TESTING.md).";
    }
}

/// <summary>
/// Gives a test its own empty MySQL database, built from the real migrations and dropped afterwards.
/// TEXTILECONNECT_TEST_MYSQL is a connection string to a server whose user may create and drop databases; the database name in it is ignored.
/// Inside <see cref="RunAsync"/>, every MarketplaceHttpFixture uses that database instead of the in-memory one.
/// Set TEXTILECONNECT_TEST_MYSQL_KEEP=1 to keep the database of a failing run for a look, or "always" to keep every one (with TEXTILECONNECT_TEST_MYSQL_KEEP_FILE the name is written to that file).
/// </summary>
public static class MySqlStore
{
    public const string Variable = "TEXTILECONNECT_TEST_MYSQL";
    private static readonly AsyncLocal<string?> Current = new();

    public static bool IsConfigured => !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(Variable));

    /// <summary>The connection string of the database of the running test, or null when the test uses the in-memory store.</summary>
    public static string? CurrentConnectionString => Current.Value;

    public static async Task RunAsync(Func<Task> test)
    {
        var server = new MySqlConnectionStringBuilder((Environment.GetEnvironmentVariable(Variable) ?? throw new InvalidOperationException($"{Variable} is not set."))
            .Replace("SslMode=None", "SslMode=Disabled", StringComparison.OrdinalIgnoreCase)) { Database = string.Empty };   // the app makes the same change (docs/RUN_LOCAL.md uses SslMode=None)
        var name = "tc_test_" + Guid.NewGuid().ToString("N")[..12];
        await using (var connection = new MySqlConnection(server.ConnectionString))
        {
            await connection.OpenAsync();
            await using var create = new MySqlCommand($"CREATE DATABASE `{name}` CHARACTER SET utf8mb4", connection);
            await create.ExecuteNonQueryAsync();
        }
        var database = new MySqlConnectionStringBuilder(server.ConnectionString) { Database = name }.ConnectionString;
        var failed = true;
        try
        {
            await using (var db = new MarketplaceDbContext(new DbContextOptionsBuilder<MarketplaceDbContext>().UseMySQL(database).Options))
            {
                await db.Database.MigrateAsync();
                await MarketplaceSeed.SeedAsync(db); // states, cities, pincodes, categories and settings, as the app does at startup
            }
            await using (var identity = new Sanfiqo.Identity.ApplicationIdentityDbContext(new DbContextOptionsBuilder<Sanfiqo.Identity.ApplicationIdentityDbContext>().UseMySQL(database).Options))
                await identity.Database.MigrateAsync();   // the sign-in tables live in the same database
            Current.Value = database;
            await test();
            failed = false;
        }
        finally
        {
            Current.Value = null;
            var keep = Environment.GetEnvironmentVariable(Variable + "_KEEP");
            if (keep == "always" && Environment.GetEnvironmentVariable(Variable + "_KEEP_FILE") is { Length: > 0 } nameFile) File.WriteAllText(nameFile, name);
            if (!(keep == "always" || failed && keep == "1"))
            {
                MySqlConnection.ClearAllPools();
                await using var connection = new MySqlConnection(server.ConnectionString);
                await connection.OpenAsync();
                await using var drop = new MySqlCommand($"DROP DATABASE IF EXISTS `{name}`", connection);
                await drop.ExecuteNonQueryAsync();
            }
        }
    }
}
