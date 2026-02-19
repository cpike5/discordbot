using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

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

/// <summary>
/// Design-time factory used by <c>dotnet ef</c> CLI to create a
/// <see cref="PostgresBotDbContext"/> for generating PostgreSQL migrations.
/// </summary>
public class PostgresDesignTimeFactory : IDesignTimeDbContextFactory<PostgresBotDbContext>
{
    public PostgresBotDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<PostgresBotDbContext>();
        optionsBuilder.UseNpgsql("Host=localhost;Database=design-time",
            x => x.MigrationsAssembly("DiscordBot.Infrastructure"));
        return new PostgresBotDbContext(optionsBuilder.Options);
    }
}
