using System.Reflection;
using DiscordBot.Infrastructure.Data;
using DiscordBot.Infrastructure.Extensions;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DiscordBot.Tests.Infrastructure.Data;

/// <summary>
/// Guards the SQLite runtime/design-time context pairing.
///
/// EF Core matches a migration to a context by the EXACT type named in its generated
/// <c>[DbContext(typeof(...))]</c> attribute. A derived context does not inherit its base's
/// migrations, and a mismatch is silent: <c>MigrateAsync</c> applies whatever subset it can see,
/// writes those rows to <c>__EFMigrationsHistory</c>, and returns successfully. Registering
/// <see cref="BotDbContext"/> instead of <see cref="SqliteBotDbContext"/> therefore produced a
/// from-scratch database missing whole tables and columns, with nothing in the log to say so.
///
/// These tests fail loudly if that pairing is broken again, or if a future migration lands under
/// <c>Migrations/Sqlite</c> attributed to a context nothing registers.
/// </summary>
public class SqliteMigrationRegistrationTests : IDisposable
{
    private readonly string _tempDirectory =
        Path.Combine(Path.GetTempPath(), $"discordbot-migtest-{Guid.NewGuid():N}");

    public SqliteMigrationRegistrationTests() => Directory.CreateDirectory(_tempDirectory);

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        try
        {
            if (Directory.Exists(_tempDirectory))
                Directory.Delete(_tempDirectory, recursive: true);
        }
        catch
        {
            // Best-effort cleanup; a file briefly held by SQLite must not fail the run.
        }
    }

    private ServiceProvider BuildProvider(string databasePath)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = $"Data Source={databasePath}",
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInfrastructure(configuration);
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }

    /// <summary>
    /// Every migration class under Migrations/Sqlite, as EF sees them: the id from
    /// <see cref="MigrationAttribute"/> paired with the context type from
    /// <see cref="DbContextAttribute"/>.
    /// </summary>
    private static IReadOnlyList<(string Id, Type? ContextType)> SqliteMigrationsInAssembly() =>
        typeof(SqliteBotDbContext).Assembly
            .GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false } && t.IsSubclassOf(typeof(Migration)))
            .Where(t => t.Namespace?.Contains("Migrations.Sqlite", StringComparison.Ordinal) == true)
            .Select(t => (
                Id: t.GetCustomAttribute<MigrationAttribute>()?.Id ?? string.Empty,
                ContextType: t.GetCustomAttribute<DbContextAttribute>()?.ContextType))
            .Where(x => x.Id.Length > 0)
            .OrderBy(x => x.Id, StringComparer.Ordinal)
            .ToList();

    [Fact]
    public void AddInfrastructure_WithSqliteConnectionString_ResolvesBotDbContextAsSqliteBotDbContext()
    {
        using var provider = BuildProvider(Path.Combine(_tempDirectory, "resolve.db"));
        using var scope = provider.CreateScope();

        var db = scope.ServiceProvider.GetRequiredService<BotDbContext>();

        db.Should().BeOfType<SqliteBotDbContext>(
            "Migrations/Sqlite is scaffolded against SqliteBotDbContext, and EF only applies "
            + "migrations whose [DbContext] attribute names the exact runtime context type");
    }

    [Fact]
    public void RegisteredSqliteContext_SeesEveryMigrationInTheActiveSqliteLineage()
    {
        var activeLineage = SqliteMigrationsInAssembly()
            .Where(m => m.ContextType == typeof(SqliteBotDbContext))
            .Select(m => m.Id)
            .ToList();

        activeLineage.Should().NotBeEmpty(
            "Migrations/Sqlite must contain migrations attributed to SqliteBotDbContext");

        using var provider = BuildProvider(Path.Combine(_tempDirectory, "lineage.db"));
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BotDbContext>();

        db.Database.GetMigrations().Should().BeEquivalentTo(activeLineage,
            "the registered context must see the whole active SQLite migration lineage - seeing "
            + "only a subset is how a from-scratch database silently comes up with missing tables");
    }

    [Fact]
    public async Task MigrateAsync_OnFreshSqliteDatabase_CreatesEveryTableAndColumnTheModelDeclares()
    {
        using var provider = BuildProvider(Path.Combine(_tempDirectory, "fresh.db"));
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BotDbContext>();

        await db.Database.MigrateAsync();

        var tables = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await using (var command = db.Database.GetDbConnection().CreateCommand())
        {
            await db.Database.OpenConnectionAsync();
            command.CommandText = "SELECT name FROM sqlite_master WHERE type = 'table'";
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                tables.Add(reader.GetString(0));
        }

        var missing = new List<string>();

        foreach (var entityType in db.Model.GetEntityTypes())
        {
            var tableName = entityType.GetTableName();
            if (tableName is null)
                continue;

            if (!tables.Contains(tableName))
            {
                missing.Add($"table {tableName}");
                continue;
            }

            var storeObject = StoreObjectIdentifier.Table(tableName, entityType.GetSchema());
            var actualColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            await using (var command = db.Database.GetDbConnection().CreateCommand())
            {
                command.CommandText = $"PRAGMA table_info(\"{tableName}\")";
                await using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                    actualColumns.Add(reader.GetString(1));
            }

            foreach (var property in entityType.GetProperties())
            {
                var columnName = property.GetColumnName(storeObject);
                if (columnName is not null && !actualColumns.Contains(columnName))
                    missing.Add($"column {tableName}.{columnName}");
            }
        }

        missing.Should().BeEmpty(
            "MigrateAsync on a from-scratch SQLite database must produce the schema the model "
            + "declares; anything missing here means EF applied only part of the migration set");
    }

    [Fact]
    public void EverySqliteMigration_IsAttributedToAContextThatIsActuallyRegistered()
    {
        var orphans = SqliteMigrationsInAssembly()
            .Where(m => m.ContextType != typeof(SqliteBotDbContext))
            .Where(m => string.CompareOrdinal(m.Id, LastSupersededMigrationId) > 0)
            .Select(m => $"{m.Id} -> {m.ContextType?.Name ?? "(no [DbContext] attribute)"}")
            .ToList();

        orphans.Should().BeEmpty(
            "a migration under Migrations/Sqlite that names any context other than "
            + "SqliteBotDbContext is invisible to the running app - scaffold it with "
            + "`--context SqliteBotDbContext` (see CLAUDE.md \"Database and migrations\")");
    }

    /// <summary>
    /// Migrations/Sqlite still carries a superseded lineage attributed to the base
    /// <see cref="BotDbContext"/> and ending here; 20260219205009 re-baselines the whole schema
    /// for <see cref="SqliteBotDbContext"/>. Those files are inert - no registered context names
    /// that type - but they are kept for history, so only migrations newer than this one are
    /// held to the attribution rule above.
    /// </summary>
    private const string LastSupersededMigrationId = "20260127225612_AddSsmlSupportToGuildTtsSettings";
}
