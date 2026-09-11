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

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        base.ConfigureConventions(configurationBuilder);

        // The existing Postgres schema stores every DateTime column as
        // `timestamp without time zone`, and the `Npgsql.EnableLegacyTimestampBehavior`
        // runtime switch in Program.cs assumes that mapping too. Starting with
        // Npgsql 10, the provider's default design-time convention for `DateTime`
        // changed to `timestamp with time zone`; without pinning it back here,
        // `dotnet ef migrations add` scaffolds a 105-column type-change migration
        // that doesn't reflect any real schema change we want to make.
        configurationBuilder.Properties<DateTime>().HaveColumnType("timestamp without time zone");
        configurationBuilder.Properties<DateTime?>().HaveColumnType("timestamp without time zone");
    }
}
