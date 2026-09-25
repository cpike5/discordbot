using System.Runtime.CompilerServices;
using DiscordBot.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace DiscordBot.Tests.TestHelpers;

/// <summary>
/// The PostgreSQL server the test suite runs against.
/// </summary>
/// <remarks>
/// <para>
/// The server comes from the <c>DISCORDBOT_TEST_POSTGRES</c> environment variable, a Npgsql
/// connection string for a role that may create databases. Unset, it is
/// <see cref="DefaultConnectionString"/>, which matches the CI service container and the web
/// session hook.
/// </para>
/// <para>
/// On first use a template database is built by applying the real PostgreSQL migrations, so a
/// migration that does not match the model fails the suite. Each test database is then a
/// <c>CREATE DATABASE ... TEMPLATE</c> clone of it, which is far cheaper than migrating again.
/// Every database this process makes is named <c>dbtest_&lt;unix seconds&gt;_...</c>; leftovers
/// from a crashed run are dropped by the next run once they are an hour old.
/// </para>
/// </remarks>
public static class PostgresTestServer
{
    /// <summary>The environment variable that overrides <see cref="DefaultConnectionString"/>.</summary>
    public const string EnvironmentVariable = "DISCORDBOT_TEST_POSTGRES";

    /// <summary>The server used when <see cref="EnvironmentVariable"/> is unset.</summary>
    public const string DefaultConnectionString = "Host=localhost;Port=5432;Username=postgres;Password=postgres";

    private const string Prefix = "dbtest_";

    private static readonly Lazy<string> Template = new(CreateTemplate, LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>
    /// Npgsql reads this switch once, on first use, and the application sets it at startup
    /// (see Program.cs). The migrations assume it, so the tests must too.
    /// </summary>
    [ModuleInitializer]
    internal static void EnableLegacyTimestampBehavior() =>
        AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

    /// <summary>Creates an empty, fully migrated database.</summary>
    public static TestDatabase CreateDatabase()
    {
        var template = Template.Value;
        var name = NewDatabaseName();
        ExecuteAdmin($"CREATE DATABASE \"{name}\" TEMPLATE \"{template}\"");
        return new TestDatabase(name, ConnectionStringFor(name));
    }

    internal static string ConnectionStringFor(string database) =>
        new NpgsqlConnectionStringBuilder(ServerConnectionString)
        {
            Database = database,
            // Pooled connections would outlive the test and keep the database from being
            // dropped; tests that forget to dispose a context would also exhaust the server.
            Pooling = false,
            IncludeErrorDetail = true
        }.ToString();

    internal static void DropDatabase(string name) =>
        ExecuteAdmin($"DROP DATABASE IF EXISTS \"{name}\" WITH (FORCE)");

    private static string ServerConnectionString =>
        Environment.GetEnvironmentVariable(EnvironmentVariable) is { Length: > 0 } configured
            ? configured
            : DefaultConnectionString;

    private static string CreateTemplate()
    {
        try
        {
            DropStaleDatabases();
        }
        catch (NpgsqlException ex)
        {
            throw new InvalidOperationException(
                $"The tests need a PostgreSQL server and could not reach one. Start one (for example " +
                $"`docker run -d -p 5432:5432 -e POSTGRES_PASSWORD=postgres postgres:16`) or point " +
                $"{EnvironmentVariable} at one. Tried: {Redact(ServerConnectionString)}",
                ex);
        }

        var name = NewDatabaseName("tpl");
        ExecuteAdmin($"CREATE DATABASE \"{name}\"");

        var options = new DbContextOptionsBuilder<PostgresBotDbContext>()
            .UseNpgsql(ConnectionStringFor(name), npgsql => npgsql.MigrationsAssembly("DiscordBot.Infrastructure"))
            .Options;
        using (var context = new PostgresBotDbContext(options))
        {
            context.Database.Migrate();
        }

        AppDomain.CurrentDomain.ProcessExit += (_, _) =>
        {
            try
            {
                DropDatabase(name);
            }
            catch (NpgsqlException)
            {
                // The next run drops it once it is stale.
            }
        };

        return name;
    }

    private static void DropStaleDatabases()
    {
        var cutoff = DateTimeOffset.UtcNow.AddHours(-1).ToUnixTimeSeconds();
        var stale = new List<string>();

        using (var connection = OpenAdmin())
        using (var command = new NpgsqlCommand("SELECT datname FROM pg_database WHERE datname LIKE 'dbtest\\_%'", connection))
        using (var reader = command.ExecuteReader())
        {
            while (reader.Read())
            {
                var name = reader.GetString(0);
                var stamp = name[Prefix.Length..].Split('_')[0];
                if (long.TryParse(stamp, out var created) && created < cutoff)
                {
                    stale.Add(name);
                }
            }
        }

        foreach (var name in stale)
        {
            DropDatabase(name);
        }
    }

    private static string NewDatabaseName(string? tag = null) =>
        $"{Prefix}{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}_{(tag is null ? "" : tag + "_")}{Guid.NewGuid():N}";

    private static void ExecuteAdmin(string sql)
    {
        using var connection = OpenAdmin();
        using var command = new NpgsqlCommand(sql, connection);
        command.ExecuteNonQuery();
    }

    private static NpgsqlConnection OpenAdmin()
    {
        var builder = new NpgsqlConnectionStringBuilder(ServerConnectionString);
        if (string.IsNullOrEmpty(builder.Database))
        {
            builder.Database = "postgres";
        }

        var connection = new NpgsqlConnection(builder.ToString());
        connection.Open();
        return connection;
    }

    private static string Redact(string connectionString) =>
        new NpgsqlConnectionStringBuilder(connectionString) { Password = null }.ToString();
}

/// <summary>
/// A throwaway PostgreSQL database, migrated and empty when created. Dispose it to drop it.
/// </summary>
public sealed class TestDatabase : IDisposable
{
    private readonly string _name;
    private bool _disposed;

    internal TestDatabase(string name, string connectionString)
    {
        _name = name;
        ConnectionString = connectionString;
        Options = new DbContextOptionsBuilder<BotDbContext>()
            .UseNpgsql(connectionString)
            .Options;
    }

    /// <summary>The Npgsql connection string for this database.</summary>
    public string ConnectionString { get; }

    /// <summary>Options for a <see cref="BotDbContext"/> on this database.</summary>
    public DbContextOptions<BotDbContext> Options { get; }

    /// <summary>Creates a context on its own connection to this database.</summary>
    public BotDbContext CreateContext() => new(Options);

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        PostgresTestServer.DropDatabase(_name);
    }
}
