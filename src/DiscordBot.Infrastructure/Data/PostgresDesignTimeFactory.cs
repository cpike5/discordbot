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
        // The app sets this switch at startup (Program.cs), and the snapshot was generated with it:
        // DateTime maps to "timestamp without time zone". Without it here, `dotnet ef` sees every
        // DateTime column as "with time zone" and scaffolds a migration that rewrites all of them.
        AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
        var optionsBuilder = new DbContextOptionsBuilder<PostgresBotDbContext>();
        optionsBuilder.UseNpgsql("Host=localhost;Database=design-time",
            x => x.MigrationsAssembly("DiscordBot.Infrastructure"));
        return new PostgresBotDbContext(optionsBuilder.Options);
    }
}
