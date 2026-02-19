using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace DiscordBot.Infrastructure.Data;

/// <summary>
/// Design-time only DbContext subclass for SQLite migrations.
/// Passes <see cref="DbContextOptions{BotDbContext}"/> to the base constructor
/// so that <c>ApplyConfigurationsFromAssembly</c> resolves correctly.
/// This class is NOT used at runtime — DI always resolves <see cref="BotDbContext"/>.
/// </summary>
public class SqliteBotDbContext : BotDbContext
{
    public SqliteBotDbContext(DbContextOptions<BotDbContext> options) : base(options)
    {
    }
}

/// <summary>
/// Design-time factory used by <c>dotnet ef</c> CLI to create a
/// <see cref="SqliteBotDbContext"/> for generating SQLite migrations.
/// </summary>
public class SqliteDesignTimeFactory : IDesignTimeDbContextFactory<SqliteBotDbContext>
{
    public SqliteBotDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<BotDbContext>();
        optionsBuilder.UseSqlite("Data Source=design-time.db",
            x => x.MigrationsAssembly("DiscordBot.Infrastructure"));
        return new SqliteBotDbContext(optionsBuilder.Options);
    }
}
