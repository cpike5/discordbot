using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace DiscordBot.Infrastructure.Data;

/// <summary>
/// Design-time factory used by <c>dotnet ef</c> CLI to create a
/// <see cref="SqliteBotDbContext"/> for generating SQLite migrations.
/// </summary>
public class SqliteDesignTimeFactory : IDesignTimeDbContextFactory<SqliteBotDbContext>
{
    public SqliteBotDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<SqliteBotDbContext>();
        optionsBuilder.UseSqlite("Data Source=design-time.db",
            x => x.MigrationsAssembly("DiscordBot.Infrastructure"));
        return new SqliteBotDbContext(optionsBuilder.Options);
    }
}
