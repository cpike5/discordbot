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
}
