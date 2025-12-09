using DiscordBot.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace DiscordBot.Infrastructure.Data;

/// <summary>
/// Entity Framework Core database context for the Discord bot.
/// </summary>
public class BotDbContext : DbContext
{
    public BotDbContext(DbContextOptions<BotDbContext> options) : base(options)
    {
    }

    public DbSet<Guild> Guilds => Set<Guild>();
    public DbSet<User> Users => Set<User>();
    public DbSet<CommandLog> CommandLogs => Set<CommandLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Apply all configurations from the current assembly
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(BotDbContext).Assembly);
    }
}
