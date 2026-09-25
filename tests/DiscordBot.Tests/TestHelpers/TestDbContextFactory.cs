using DiscordBot.Infrastructure.Data;

namespace DiscordBot.Tests.TestHelpers;

/// <summary>
/// Creates PostgreSQL-backed <see cref="BotDbContext"/> instances for tests. Every call gets its
/// own freshly cloned database (see <see cref="PostgresTestServer"/>), so tests stay isolated and
/// can run in parallel.
/// </summary>
public static class TestDbContextFactory
{
    /// <summary>
    /// Creates a new database and a context on it.
    /// </summary>
    /// <returns>The context, and the database that owns it.</returns>
    /// <remarks>
    /// The caller disposes both. Disposing the database drops it.
    /// </remarks>
    public static (BotDbContext context, TestDatabase database) CreateContext()
    {
        var database = CreateDatabase();
        return (database.CreateContext(), database);
    }

    /// <summary>
    /// Creates a new database with no context open on it. Use this when a test needs several
    /// contexts at once, for example to exercise concurrent writers, or when it only needs the
    /// <see cref="TestDatabase.Options"/> to hand to a factory.
    /// </summary>
    public static TestDatabase CreateDatabase() => PostgresTestServer.CreateDatabase();
}
