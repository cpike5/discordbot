using System.Data.Common;
using System.Text;
using DiscordBot.Infrastructure.Data;
using DiscordBot.Infrastructure.Data.Migrations;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace DiscordBot.Tests.Infrastructure.Data;

/// <summary>
/// Covers the upgrade path for SQLite databases created before the SqliteBotDbContext registration
/// fix. Every test here is file-backed on purpose: the repair reads <c>__EFMigrationsHistory</c>
/// and <c>sqlite_master</c> across more than one connection, which an in-memory database cannot
/// model.
///
/// The legacy fixture is produced exactly the way the bug produced it - by migrating the base
/// <see cref="BotDbContext"/> against SQLite, which EF matches only to the 40 superseded
/// <c>[DbContext(typeof(BotDbContext))]</c> migrations - so these tests exercise the real
/// pre-fix on-disk state rather than a hand-built imitation.
/// </summary>
public class SqliteLegacyHistoryRepairTests : IDisposable
{
    private readonly string _tempDirectory =
        Path.Combine(Path.GetTempPath(), $"discordbot-repair-{Guid.NewGuid():N}");

    public SqliteLegacyHistoryRepairTests() => Directory.CreateDirectory(_tempDirectory);

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        SqliteConnectionPoolReset();
        try
        {
            if (Directory.Exists(_tempDirectory))
                Directory.Delete(_tempDirectory, recursive: true);
        }
        catch
        {
            // Best-effort cleanup; a file still held by SQLite must not fail the run.
        }
    }

    private static void SqliteConnectionPoolReset() => Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();

    private string PathFor(string name) => Path.Combine(_tempDirectory, name);

    private static BotDbContext OpenLegacyContext(string path) =>
        new(new DbContextOptionsBuilder<BotDbContext>()
            .UseSqlite($"Data Source={path}", b => b.MigrationsAssembly("DiscordBot.Infrastructure"))
            .Options);

    private static SqliteBotDbContext OpenCurrentContext(string path) =>
        new(new DbContextOptionsBuilder<SqliteBotDbContext>()
            .UseSqlite($"Data Source={path}", b => b.MigrationsAssembly("DiscordBot.Infrastructure"))
            .Options);

    /// <summary>
    /// Reproduces a pre-fix database: the base context sees only the superseded lineage, so
    /// <c>Migrate()</c> applies those 40 migrations and stops.
    /// </summary>
    private static void CreateLegacyDatabase(string path)
    {
        using var db = OpenLegacyContext(path);
        db.Database.Migrate();
    }

    private static async Task<string> CreateFreshDatabaseAsync(string path)
    {
        await using var db = OpenCurrentContext(path);
        await db.Database.MigrateAsync();
        return path;
    }

    private static async Task<bool> RepairAsync(DbContext db) =>
        await SqliteLegacyHistoryRepair.RepairAsync(db, NullLogger.Instance);

    // ---------------------------------------------------------------- schema capture

    /// <summary>
    /// A comparable rendering of a database's schema: every <c>sqlite_master</c> object name by
    /// type, and every table's column set.
    ///
    /// Column <b>defaults</b> are deliberately excluded. A legacy database carries DEFAULT clauses
    /// on <c>GuildAudioSettings.SilentPlayback</c> and <c>MetricSnapshots.CpuUsagePercent</c> that a
    /// from-scratch database does not, because those columns arrived via <c>AddColumn</c> with a
    /// <c>defaultValue:</c> while the re-baseline's <c>CreateTable</c> declares them plainly - and
    /// SQLite cannot drop a DEFAULT without rebuilding the table. The repair adds one of its own for
    /// the same reason (see <c>SqliteLegacyHistoryRepair</c>). Defaults are invisible to EF, which
    /// always writes these columns explicitly; names, types, nullability and keys are what must
    /// match, and those are compared in full.
    /// </summary>
    private static async Task<string> CaptureSchemaAsync(DbContext db)
    {
        var sb = new StringBuilder();
        var connection = db.Database.GetDbConnection();
        await db.Database.OpenConnectionAsync();

        var objects = new List<(string Type, string Name)>();
        await using (var command = connection.CreateCommand())
        {
            command.CommandText =
                "SELECT type, name FROM sqlite_master WHERE name NOT LIKE 'sqlite_%' ORDER BY type, name";
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                objects.Add((reader.GetString(0), reader.GetString(1)));
        }

        foreach (var (type, name) in objects)
        {
            // The history table's contents differ by design (a repaired database keeps its legacy
            // rows); its own shape is created by EF and identical either way.
            if (name == "__EFMigrationsHistory")
                continue;

            sb.Append(type).Append('\t').Append(name).AppendLine();
            if (type != "table")
                continue;

            var columns = new List<string>();
            await using var pragma = connection.CreateCommand();
            pragma.CommandText = $"PRAGMA table_info(\"{name}\")";
            await using var columnReader = await pragma.ExecuteReaderAsync();
            while (await columnReader.ReadAsync())
            {
                columns.Add(
                    $"{columnReader.GetString(1)}:{columnReader.GetString(2)}"
                    + $":notnull={columnReader.GetInt32(3)}:pk={columnReader.GetInt32(5)}");
            }

            columns.Sort(StringComparer.Ordinal);
            foreach (var column in columns)
                sb.Append("  col\t").Append(name).Append('.').Append(column).AppendLine();
        }

        return sb.ToString();
    }

    private static async Task<IReadOnlyList<string>> HistoryAsync(DbContext db)
    {
        var ids = new List<string>();
        var connection = db.Database.GetDbConnection();
        await db.Database.OpenConnectionAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT MigrationId FROM __EFMigrationsHistory ORDER BY MigrationId";
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            ids.Add(reader.GetString(0));
        return ids;
    }

    private static async Task<bool> TableExistsAsync(DbContext db, string table)
    {
        var connection = db.Database.GetDbConnection();
        await db.Database.OpenConnectionAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = '{table}'";
        return Convert.ToInt64(await command.ExecuteScalarAsync()) > 0;
    }

    private static async Task<long> ScalarAsync(DbContext db, string sql)
    {
        var connection = db.Database.GetDbConnection();
        await db.Database.OpenConnectionAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        return Convert.ToInt64(await command.ExecuteScalarAsync());
    }

    private static async Task ExecuteAsync(DbContext db, string sql)
    {
        var connection = db.Database.GetDbConnection();
        await db.Database.OpenConnectionAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }

    // ---------------------------------------------------------------- tests

    [Fact]
    public async Task LegacyFixture_HasOnlyTheSupersededLineage()
    {
        var path = PathFor("fixture.db");
        CreateLegacyDatabase(path);

        await using var db = OpenCurrentContext(path);
        var history = await HistoryAsync(db);

        history.Should().HaveCount(40,
            "the pre-fix registration matched only the [DbContext(typeof(BotDbContext))] migrations");
        history.Should().Contain(SqliteLegacyHistoryRepair.LastLegacyMigrationId);
        history.Should().NotContain(SqliteLegacyHistoryRepair.BaselineMigrationId);

        (await TableExistsAsync(db, "FeatureRequests")).Should().BeFalse(
            "AddFeatureRequests belongs to the SqliteBotDbContext lineage the old registration never saw");
    }

    [Fact]
    public async Task MigrateAsync_OnLegacyDatabaseWithoutRepair_Throws()
    {
        var path = PathFor("unrepaired.db");
        CreateLegacyDatabase(path);

        await using var db = OpenCurrentContext(path);

        var act = async () => await db.Database.MigrateAsync();

        (await act.Should().ThrowAsync<DbException>())
            .WithMessage("*already exists*",
                "this is the boot crash the repair exists to prevent - without it the re-baseline "
                + "tries to CREATE TABLE over the schema the legacy chain already built");
    }

    [Fact]
    public async Task RepairThenMigrate_OnLegacyDatabase_SucceedsAndAppliesTheWholeCurrentLineage()
    {
        var path = PathFor("repaired.db");
        CreateLegacyDatabase(path);

        await using var db = OpenCurrentContext(path);

        var repaired = await RepairAsync(db);
        repaired.Should().BeTrue("the fixture is exactly the pre-fix state the repair targets");

        var act = async () => await db.Database.MigrateAsync();
        await act.Should().NotThrowAsync();

        var history = await HistoryAsync(db);
        history.Should().Contain(db.Database.GetMigrations(),
            "every migration in the SqliteBotDbContext lineage must end up applied");
        history.Should().Contain(SqliteLegacyHistoryRepair.LastLegacyMigrationId,
            "the legacy rows are left in place as a record of how the database was built");
    }

    [Fact]
    public async Task RepairedDatabase_HasTheSameSchemaAsOneMigratedFromScratch()
    {
        var legacyPath = PathFor("schema-legacy.db");
        CreateLegacyDatabase(legacyPath);

        string repairedSchema;
        await using (var db = OpenCurrentContext(legacyPath))
        {
            await RepairAsync(db);
            await db.Database.MigrateAsync();
            repairedSchema = await CaptureSchemaAsync(db);
        }

        string freshSchema;
        await using (var db = OpenCurrentContext(await CreateFreshDatabaseAsync(PathFor("schema-fresh.db"))))
        {
            freshSchema = await CaptureSchemaAsync(db);
        }

        repairedSchema.Should().Be(freshSchema,
            "an upgraded database must be indistinguishable from a new one - every table, index, "
            + "trigger and view, and every column's name, type, nullability and key role");
    }

    [Fact]
    public async Task Repair_OnFreshDatabase_IsANoOp()
    {
        var path = await CreateFreshDatabaseAsync(PathFor("fresh.db"));

        await using var db = OpenCurrentContext(path);
        var before = await HistoryAsync(db);

        (await RepairAsync(db)).Should().BeFalse(
            "a from-scratch database already starts at the re-baseline");

        (await HistoryAsync(db)).Should().BeEquivalentTo(before);
    }

    [Fact]
    public async Task Repair_OnBrandNewEmptyFile_IsANoOpAndLeavesMigrationToEf()
    {
        var path = PathFor("empty.db");

        await using var db = OpenCurrentContext(path);
        (await RepairAsync(db)).Should().BeFalse("there is no history table to repair yet");

        var act = async () => await db.Database.MigrateAsync();
        await act.Should().NotThrowAsync();

        (await TableExistsAsync(db, "FeatureRequests")).Should().BeTrue();
    }

    [Fact]
    public async Task Repair_RunTwice_IsIdempotent()
    {
        var path = PathFor("twice.db");
        CreateLegacyDatabase(path);

        await using var db = OpenCurrentContext(path);

        (await RepairAsync(db)).Should().BeTrue();
        var afterFirst = await HistoryAsync(db);

        (await RepairAsync(db)).Should().BeFalse(
            "the second pass sees the re-baseline already recorded and must change nothing");
        (await HistoryAsync(db)).Should().BeEquivalentTo(afterFirst);

        (await ScalarAsync(db,
                "SELECT COUNT(*) FROM __EFMigrationsHistory WHERE MigrationId = "
                + $"'{SqliteLegacyHistoryRepair.BaselineMigrationId}'"))
            .Should().Be(1, "the re-baseline must not be recorded twice");

        var act = async () => await db.Database.MigrateAsync();
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Repair_AddsTheOneColumnTheReBaselineIntroduces_DefaultingExistingRowsToEnabled()
    {
        var path = PathFor("isenabled.db");
        CreateLegacyDatabase(path);

        await using (var seed = OpenCurrentContext(path))
        {
            await ExecuteAsync(seed,
                "INSERT INTO Guilds (Id, Name, IsActive, JoinedAt) VALUES (42, 'Fixture', 1, CURRENT_TIMESTAMP)");
            await ExecuteAsync(seed,
                """
                INSERT INTO GuildModerationConfigs (GuildId, Mode, SimplePreset, SpamConfig, ContentFilterConfig, RaidProtectionConfig, UpdatedAt)
                VALUES (42, 0, NULL, '{}', '{}', '{}', CURRENT_TIMESTAMP)
                """);
        }

        await using var db = OpenCurrentContext(path);
        await RepairAsync(db);

        (await ScalarAsync(db, "SELECT IsEnabled FROM GuildModerationConfigs WHERE GuildId = 42"))
            .Should().Be(1,
                "a guild that had moderation configured before the flag existed must stay moderated");
    }

    [Fact]
    public async Task RepairThenMigrate_PreservesExistingData()
    {
        var path = PathFor("data.db");
        CreateLegacyDatabase(path);

        await using (var seed = OpenCurrentContext(path))
        {
            await ExecuteAsync(seed,
                "INSERT INTO Guilds (Id, Name, IsActive, JoinedAt) VALUES (1234567890123456789, 'Keep Me', 1, CURRENT_TIMESTAMP)");
            await ExecuteAsync(seed,
                """
                INSERT INTO ApplicationSettings (Key, Value, Category, DataType, RequiresRestart, LastModifiedAt, LastModifiedBy)
                VALUES ('upgrade.probe', 'survived', 0, 0, 0, CURRENT_TIMESTAMP, NULL)
                """);
        }

        await using var db = OpenCurrentContext(path);
        await RepairAsync(db);
        await db.Database.MigrateAsync();

        (await ScalarAsync(db, "SELECT COUNT(*) FROM Guilds WHERE Id = 1234567890123456789 AND Name = 'Keep Me'"))
            .Should().Be(1, "the repair must never rebuild or drop a table that holds data");
        (await ScalarAsync(db, "SELECT COUNT(*) FROM ApplicationSettings WHERE Key = 'upgrade.probe' AND Value = 'survived'"))
            .Should().Be(1);
    }

    [Fact]
    public async Task RepairThenMigrate_SeedsThemesAndAlertConfigsWithoutDuplicating()
    {
        var legacyPath = PathFor("seeds-legacy.db");
        CreateLegacyDatabase(legacyPath);

        await using (var db = OpenCurrentContext(legacyPath))
        {
            // The legacy chain seeded both tables; the two seed migrations use INSERT OR IGNORE so
            // that re-running them over those rows is a no-op rather than a constraint failure.
            var themesBefore = await ScalarAsync(db, "SELECT COUNT(*) FROM Themes");
            var alertsBefore = await ScalarAsync(db, "SELECT COUNT(*) FROM PerformanceAlertConfigs");
            themesBefore.Should().BeGreaterThan(0);
            alertsBefore.Should().BeGreaterThan(0);

            await RepairAsync(db);
            await db.Database.MigrateAsync();

            (await ScalarAsync(db, "SELECT COUNT(*) FROM Themes")).Should().Be(themesBefore);
            (await ScalarAsync(db, "SELECT COUNT(*) FROM PerformanceAlertConfigs")).Should().Be(alertsBefore);
        }

        await using var fresh = OpenCurrentContext(await CreateFreshDatabaseAsync(PathFor("seeds-fresh.db")));
        (await ScalarAsync(fresh, "SELECT COUNT(*) FROM Themes")).Should().Be(2,
            "SeedDefaultThemesSqlite restores the rows the re-baseline dropped");
        (await ScalarAsync(fresh, "SELECT COUNT(*) FROM PerformanceAlertConfigs")).Should().Be(8,
            "SeedPerformanceAlertConfigsSqlite restores the eight defaults, matching InitialPostgresql");
    }

    [Fact]
    public async Task Repair_OnUnrecognisedHistory_StandsAsideWithoutStamping()
    {
        var path = PathFor("unknown.db");
        CreateLegacyDatabase(path);

        await using var db = OpenCurrentContext(path);
        await ExecuteAsync(db,
            "DELETE FROM __EFMigrationsHistory WHERE MigrationId = "
            + $"'{SqliteLegacyHistoryRepair.LastLegacyMigrationId}'");

        (await RepairAsync(db)).Should().BeFalse(
            "neither marker is present, so the repair cannot know what schema it is looking at");

        (await HistoryAsync(db)).Should().NotContain(SqliteLegacyHistoryRepair.BaselineMigrationId,
            "standing aside must never record the re-baseline as applied");
    }

    [Fact]
    public async Task Repair_WhenLegacyHistoryDoesNotMatchTheSchema_ThrowsRatherThanStamp()
    {
        var path = PathFor("mismatch.db");
        CreateLegacyDatabase(path);

        await using var db = OpenCurrentContext(path);
        // Simulate a database whose history claims the legacy chain but whose schema is missing one
        // of the objects that chain builds.
        await ExecuteAsync(db, "DROP TABLE SoundPlayLogs");

        var act = async () => await RepairAsync(db);

        (await act.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("*SoundPlayLogs*",
                "stamping the re-baseline over a schema that is missing objects would hide them "
                + "from every future migration");

        (await HistoryAsync(db)).Should().NotContain(SqliteLegacyHistoryRepair.BaselineMigrationId);
    }
}
