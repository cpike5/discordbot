using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace DiscordBot.Infrastructure.Data;

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
