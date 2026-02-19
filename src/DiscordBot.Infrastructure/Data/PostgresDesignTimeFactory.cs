using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace DiscordBot.Infrastructure.Data;

/// <summary>
/// Design-time only DbContext subclass for PostgreSQL migrations.
/// Passes <see cref="DbContextOptions{BotDbContext}"/> to the base constructor
/// so that <c>ApplyConfigurationsFromAssembly</c> resolves correctly.
/// This class is NOT used at runtime — DI always resolves <see cref="BotDbContext"/>.
/// </summary>
public class PostgresBotDbContext : BotDbContext
{
    public PostgresBotDbContext(DbContextOptions<BotDbContext> options) : base(options)
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
        var optionsBuilder = new DbContextOptionsBuilder<BotDbContext>();
        optionsBuilder.UseNpgsql("Host=localhost;Database=design-time",
            x => x.MigrationsAssembly("DiscordBot.Infrastructure"));
        return new PostgresBotDbContext(optionsBuilder.Options);
    }
}
