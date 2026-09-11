using DiscordBot.Infrastructure.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace DiscordBot.Tests.TestHelpers;

/// <summary>
/// Factory for creating in-memory SQLite database contexts for testing.
/// </summary>
public static class TestDbContextFactory
{
    /// <summary>
    /// Creates a new BotDbContext instance with an in-memory SQLite database.
    /// The database is isolated per connection and will be destroyed when the connection is closed.
    /// </summary>
    /// <returns>A tuple containing the DbContext and the underlying connection that must be kept open.</returns>
    /// <remarks>
    /// The caller is responsible for disposing both the context and the connection.
    /// The connection must remain open for the duration of the test, otherwise the in-memory database will be lost.
    /// </remarks>
    public static (BotDbContext context, SqliteConnection connection) CreateContext()
    {
        // Create an in-memory SQLite connection
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        // Configure DbContext to use the in-memory connection
        var options = new DbContextOptionsBuilder<BotDbContext>()
            .UseSqlite(connection)
            .Options;

        var context = new BotDbContext(options);

        // Create the database schema
        context.Database.EnsureCreated();

        return (context, connection);
    }

    /// <summary>
    /// Creates a file-backed SQLite database that several <see cref="BotDbContext"/> instances, each
    /// on its own connection, can hit at once.
    /// </summary>
    /// <remarks>
    /// The <c>:memory:</c> database above lives inside one connection, so it cannot be used to test
    /// anything concurrent. This one is a throwaway file in the temp directory, in WAL mode so a
    /// waiting writer never blocks the committing one, and with a busy timeout so the second writer
    /// waits its turn instead of failing. The caller disposes it, which deletes the file.
    /// </remarks>
    public static SharedTestDatabase CreateSharedDatabase() => new();
}

/// <summary>
/// A temporary file-backed SQLite database shared by several contexts. Dispose it to delete the
/// file.
/// </summary>
public sealed class SharedTestDatabase : IDisposable
{
    private readonly string _path;
    private readonly SqliteConnection _keepAlive;
    private readonly DbContextOptions<BotDbContext> _options;

    internal SharedTestDatabase()
    {
        _path = Path.Combine(Path.GetTempPath(), $"discordbot-tests-{Guid.NewGuid():N}.db");

        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = _path,
            // Microsoft.Data.Sqlite turns this into sqlite3_busy_timeout, so a writer that finds
            // the database locked waits rather than throwing.
            DefaultTimeout = 30
        }.ToString();

        _options = new DbContextOptionsBuilder<BotDbContext>()
            .UseSqlite(connectionString)
            .Options;

        // Held open for the lifetime of the database so the WAL pragma and the schema survive
        // individual contexts coming and going.
        _keepAlive = new SqliteConnection(connectionString);
        _keepAlive.Open();

        using (var pragma = _keepAlive.CreateCommand())
        {
            pragma.CommandText = "PRAGMA journal_mode=WAL;";
            pragma.ExecuteNonQuery();
        }

        using var context = new BotDbContext(_options);
        context.Database.EnsureCreated();
    }

    /// <summary>Creates a context on its own connection to the shared database.</summary>
    public BotDbContext CreateContext() => new(_options);

    public void Dispose()
    {
        _keepAlive.Dispose();
        SqliteConnection.ClearAllPools();

        foreach (var file in new[] { _path, _path + "-wal", _path + "-shm" })
        {
            try
            {
                if (File.Exists(file))
                {
                    File.Delete(file);
                }
            }
            catch (IOException)
            {
                // A throwaway file in the temp directory; losing the race to delete it is harmless.
            }
        }
    }
}
