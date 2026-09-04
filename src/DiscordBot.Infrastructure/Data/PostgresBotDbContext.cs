using Microsoft.EntityFrameworkCore;

namespace DiscordBot.Infrastructure.Data;

/// <summary>
/// DbContext subclass for PostgreSQL. Used at runtime when the PostgreSQL provider
/// is selected, and at design-time for generating PostgreSQL migrations.
/// </summary>
public class PostgresBotDbContext : BotDbContext
{
    public PostgresBotDbContext(DbContextOptions<PostgresBotDbContext> options) : base(options)
    {
    }
}
