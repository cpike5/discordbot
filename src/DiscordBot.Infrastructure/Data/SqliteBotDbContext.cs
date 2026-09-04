using Microsoft.EntityFrameworkCore;

namespace DiscordBot.Infrastructure.Data;

/// <summary>
/// DbContext subclass for SQLite. Used at design-time for generating SQLite migrations.
/// </summary>
public class SqliteBotDbContext : BotDbContext
{
    public SqliteBotDbContext(DbContextOptions<SqliteBotDbContext> options) : base(options)
    {
    }
}
