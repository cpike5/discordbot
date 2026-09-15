using System.Data.Common;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.Extensions.Logging;

namespace DiscordBot.Infrastructure.Data.Migrations;

/// <summary>
/// One-shot startup repair for SQLite databases created before the
/// <see cref="SqliteBotDbContext"/> registration fix.
///
/// <para><b>What went wrong.</b> <c>Migrations/Sqlite</c> holds two lineages: 40 migrations
/// attributed to the base <see cref="BotDbContext"/>, ending at <see cref="LastLegacyMigrationId"/>,
/// and the current lineage attributed to <see cref="SqliteBotDbContext"/>, starting at
/// <see cref="BaselineMigrationId"/> - a from-scratch re-baseline that re-creates the whole schema.
/// Infrastructure used to register the base <see cref="BotDbContext"/> for SQLite, and EF matches a
/// migration to a context by exact runtime type, so those installs only ever applied the 40 legacy
/// migrations and wrote those 40 ids to <c>__EFMigrationsHistory</c>.</para>
///
/// <para><b>Why it needs repairing.</b> Now that the correct context is registered, the migrator
/// sees none of those 40 ids (they belong to the other lineage) and treats the re-baseline as
/// pending. Its first statement is <c>CREATE TABLE "ApplicationSettings"</c> against a database
/// that already has that table, so <c>MigrateAsync()</c> - which <c>Program.cs</c> calls unguarded
/// at boot - throws <c>SQLite Error 1: 'table "ApplicationSettings" already exists'</c> and the
/// process never starts.</para>
///
/// <para><b>The delta.</b> The legacy chain's end state and the re-baseline's end state were
/// compared object by object (<c>sqlite_master</c> plus <c>PRAGMA table_info</c> across the legacy
/// chain's 55 tables and the current lineage's 76). They are identical except for one column: <c>GuildModerationConfigs.IsEnabled</c>,
/// which is what the re-baseline migration is actually named for. Every table, index and other
/// column already matches, because the re-baseline was scaffolded as a full snapshot of the schema
/// the legacy chain had already built. So the repair adds that one column, then stamps the
/// re-baseline as applied; the migrations after it only add new tables and columns and run
/// normally.</para>
///
/// <para>The 40 legacy history rows are deliberately left in place. EF ignores ids it does not
/// recognise, and keeping them preserves the record of how the database was actually built.</para>
/// </summary>
public static class SqliteLegacyHistoryRepair
{
    /// <summary>Last migration in the superseded <see cref="BotDbContext"/>-attributed lineage.</summary>
    public const string LastLegacyMigrationId = "20260127225612_AddSsmlSupportToGuildTtsSettings";

    /// <summary>
    /// First migration in the <see cref="SqliteBotDbContext"/> lineage - a full re-baseline of the
    /// schema the legacy chain had already produced, plus <c>GuildModerationConfigs.IsEnabled</c>.
    /// </summary>
    public const string BaselineMigrationId = "20260219205009_AddIsEnabledToGuildModerationConfig";

    private const string HistoryTable = "__EFMigrationsHistory";

    /// <summary>
    /// Shared actionable text for the two states this repair refuses to guess its way through: a
    /// history with no recognised ids at all (stood aside, logged at Warning), and one with *some*
    /// but not all of the legacy chain's ids (thrown, since guessing which end state that is risks
    /// silently hiding schema objects or losing data).
    /// </summary>
    private const string RecoveryGuidance =
        "Back up the database, then either run `dotnet ef database update --context SqliteBotDbContext` "
        + "(--project src/DiscordBot.Infrastructure --startup-project src/DiscordBot.Bot) to try applying "
        + "the rest of the migrations by hand, or delete the file and let a fresh database be created.";

    /// <summary>
    /// The complete schema delta between the legacy chain's end state and the re-baseline, as
    /// derived above. <c>IsEnabled</c> is declared NOT NULL with no store default, but SQLite
    /// cannot add a NOT NULL column without one, so the repair supplies <c>1</c> - matching
    /// <c>GuildModerationConfig.IsEnabled</c>'s CLR initialiser, and keeping moderation on for
    /// guilds that had it configured before the flag existed. The residual <c>DEFAULT 1</c> clause
    /// is invisible to EF, which always writes the column explicitly.
    /// </summary>
    private const string AddIsEnabledSql =
        """ALTER TABLE "GuildModerationConfigs" ADD COLUMN "IsEnabled" INTEGER NOT NULL DEFAULT 1;""";

    /// <summary>
    /// Brings a pre-fix SQLite database to the state the re-baseline would have left it in, so that
    /// the caller's <c>MigrateAsync()</c> can carry on normally. Safe and cheap to call on every
    /// boot: it is a no-op on a fresh database, on an already-repaired one, and on any non-SQLite
    /// provider.
    /// </summary>
    /// <param name="context">The context about to be migrated.</param>
    /// <param name="logger">Logger; the repair reports at Warning when it changes anything.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns><c>true</c> if the database was repaired, <c>false</c> if nothing needed doing.</returns>
    /// <exception cref="InvalidOperationException">
    /// The database carries pre-split history but its schema is not the one the legacy chain
    /// produces, so stamping the re-baseline would permanently hide missing tables or columns.
    /// Thrown in preference to writing a history row that lies about the schema.
    /// </exception>
    public static async Task<bool> RepairAsync(
        DbContext context,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(logger);

        if (!context.Database.IsSqlite())
            return false;

        var connection = context.Database.GetDbConnection();
        var wasClosed = connection.State != System.Data.ConnectionState.Open;
        if (wasClosed)
            await context.Database.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            if (!await TableExistsAsync(connection, HistoryTable, cancellationToken).ConfigureAwait(false))
                return false; // Brand-new database: MigrateAsync builds it from the baseline.

            var applied = await ReadAppliedMigrationsAsync(connection, cancellationToken).ConfigureAwait(false);

            if (applied.Count == 0)
                return false; // History table exists but is empty - still a from-scratch migration.

            if (applied.ContainsKey(BaselineMigrationId))
                return false; // Already on the current lineage (fresh, or repaired on an earlier boot).

            if (!applied.ContainsKey(LastLegacyMigrationId))
            {
                var legacyIds = GetLegacyMigrationIds();
                var recognisedLegacyCount = applied.Keys.Count(legacyIds.Contains);

                if (recognisedLegacyCount == 0)
                {
                    // Not a single id in this history belongs to either lineage - a state this
                    // repair genuinely cannot reason about. Say so loudly and stand aside rather
                    // than guess; MigrateAsync will report its own error next.
                    logger.LogWarning(
                        "SQLite database has {Count} migration(s) applied but none recognised from the "
                        + "superseded BotDbContext lineage or the current SqliteBotDbContext lineage "
                        + "({Baseline}). The pre-split history repair does not recognise this state and is "
                        + "standing aside. {Guidance}",
                        applied.Count, BaselineMigrationId, RecoveryGuidance);
                    return false;
                }

                // Some, but not all, of the legacy chain's ids are present (e.g. 35 of 40) - the
                // history has been edited or corrupted somehow. Guessing which end state this is
                // would risk stamping the re-baseline over a schema it doesn't actually match, so
                // refuse outright instead of failing open.
                throw new InvalidOperationException(
                    $"SQLite database has {applied.Count} migration(s) applied, {recognisedLegacyCount} of "
                    + $"{legacyIds.Count} recognised from the superseded BotDbContext lineage, but it does not "
                    + $"end at {LastLegacyMigrationId} and has not reached {BaselineMigrationId} either. This is "
                    + $"not a state the pre-split history repair can safely act on - {RecoveryGuidance}");
            }

            VerifyLegacySchemaMatchesBaseline(connection, cancellationToken);

            var productVersion = applied[LastLegacyMigrationId];
            var addedColumn = false;

            await using (var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false))
            {
                if (!await ColumnExistsAsync(connection, transaction, "GuildModerationConfigs", "IsEnabled", cancellationToken).ConfigureAwait(false))
                {
                    await ExecuteAsync(connection, transaction, AddIsEnabledSql, cancellationToken).ConfigureAwait(false);
                    addedColumn = true;
                }

                await using (var insert = connection.CreateCommand())
                {
                    insert.Transaction = transaction;
                    insert.CommandText =
                        $"""INSERT INTO "{HistoryTable}" ("MigrationId", "ProductVersion") VALUES ($id, $version);""";
                    AddParameter(insert, "$id", BaselineMigrationId);
                    AddParameter(insert, "$version", productVersion);
                    await insert.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                }

                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            }

            // Mirror the row just inserted so the "remaining" count below reflects what MigrateAsync
            // will actually see next, rather than assuming the baseline is the only current-lineage
            // id GetMigrations() will report (true by construction, but worth computing rather than
            // asserting via a bare "- 1").
            applied[BaselineMigrationId] = productVersion;

            logger.LogWarning(
                "Repaired a SQLite database created before the SqliteBotDbContext registration fix: "
                + "its history ends at {Legacy} (the superseded BotDbContext lineage, {Count} migrations), "
                + "so {Baseline} was recorded as already applied{Column}. The {Remaining} migration(s) after it "
                + "will now apply normally. The legacy history rows were left in place. "
                + "See docs/lessons-learned/sqlite-migration-context-mismatch.md.",
                LastLegacyMigrationId,
                applied.Count,
                BaselineMigrationId,
                addedColumn
                    ? " after adding the one column it introduces, GuildModerationConfigs.IsEnabled"
                    : " (GuildModerationConfigs.IsEnabled was already present)",
                context.Database.GetMigrations().Except(applied.Keys).Count());

            return true;
        }
        finally
        {
            if (wasClosed)
                await context.Database.CloseConnectionAsync().ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Confirms the database really is at the legacy chain's end state before anything is stamped:
    /// every table and column the re-baseline creates must already exist, apart from the single
    /// column of the known delta. Reads the expectation from the re-baseline migration's own
    /// operations rather than a hand-copied list, so it keeps telling the truth if that file ever
    /// changes.
    /// </summary>
    private static void VerifyLegacySchemaMatchesBaseline(DbConnection connection, CancellationToken cancellationToken)
    {
        var baseline = FindBaselineMigration()
            ?? throw new InvalidOperationException(
                $"Could not find the {BaselineMigrationId} migration class in this assembly to verify the "
                + "legacy database's schema against before stamping it as applied. A missing migration class "
                + "the repair depends on is a build/deployment bug, not a state to silently skip verification for.");

        var existingTables = ReadTableNames(connection, cancellationToken);
        var problems = new List<string>();

        foreach (var create in baseline.UpOperations.OfType<CreateTableOperation>())
        {
            if (!existingTables.Contains(create.Name))
            {
                problems.Add($"table {create.Name}");
                continue;
            }

            var columns = ReadColumnNames(connection, create.Name, cancellationToken);
            foreach (var column in create.Columns)
            {
                if (columns.Contains(column.Name))
                    continue;

                // The one column the re-baseline genuinely adds; the repair creates it below.
                if (create.Name == "GuildModerationConfigs" && column.Name == "IsEnabled")
                    continue;

                problems.Add($"column {create.Name}.{column.Name}");
            }
        }

        if (problems.Count > 0)
        {
            throw new InvalidOperationException(
                $"This SQLite database's history ends at {LastLegacyMigrationId}, but its schema is not the one "
                + $"that lineage produces - {problems.Count} object(s) the {BaselineMigrationId} re-baseline "
                + $"expects are missing: {string.Join(", ", problems.Take(20))}"
                + (problems.Count > 20 ? ", ..." : string.Empty)
                + ". Refusing to record the re-baseline as applied, because that would hide those objects from "
                + "every future migration. Restore the database from backup and upgrade through an earlier "
                + "release, or recreate it. See docs/lessons-learned/sqlite-migration-context-mismatch.md.");
        }
    }

    private static Migration? FindBaselineMigration()
    {
        var type = typeof(SqliteBotDbContext).Assembly
            .GetTypes()
            .FirstOrDefault(t => t is { IsClass: true, IsAbstract: false }
                && t.IsSubclassOf(typeof(Migration))
                && t.GetCustomAttribute<MigrationAttribute>()?.Id == BaselineMigrationId);

        if (type is null || Activator.CreateInstance(type) is not Migration migration)
            return null;

        migration.ActiveProvider = "Microsoft.EntityFrameworkCore.Sqlite";
        return migration;
    }

    /// <summary>
    /// Every migration id attributed to the superseded base <see cref="BotDbContext"/> - the 40
    /// ids a pre-fix database's history can legitimately contain. Read via reflection (the same
    /// way <see cref="FindBaselineMigration"/> finds the re-baseline) rather than hand-maintained,
    /// so it can never drift from the migration classes actually in this assembly.
    /// </summary>
    private static HashSet<string> GetLegacyMigrationIds()
    {
        return typeof(SqliteBotDbContext).Assembly
            .GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false } && t.IsSubclassOf(typeof(Migration)))
            .Where(t => t.GetCustomAttribute<DbContextAttribute>()?.ContextType == typeof(BotDbContext))
            .Select(t => t.GetCustomAttribute<MigrationAttribute>()?.Id)
            .Where(id => id is not null)
            .Select(id => id!)
            .ToHashSet(StringComparer.Ordinal);
    }

    private static async Task<bool> TableExistsAsync(DbConnection connection, string table, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = $name;";
        AddParameter(command, "$name", table);
        var result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return Convert.ToInt64(result) > 0;
    }

    private static async Task<Dictionary<string, string>> ReadAppliedMigrationsAsync(
        DbConnection connection, CancellationToken cancellationToken)
    {
        var applied = new Dictionary<string, string>(StringComparer.Ordinal);
        await using var command = connection.CreateCommand();
        command.CommandText = $"""SELECT "MigrationId", "ProductVersion" FROM "{HistoryTable}";""";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            applied[reader.GetString(0)] = reader.IsDBNull(1) ? string.Empty : reader.GetString(1);
        return applied;
    }

    private static async Task<bool> ColumnExistsAsync(
        DbConnection connection, DbTransaction transaction, string table, string column, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"""PRAGMA table_info("{table}");""";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            if (string.Equals(reader.GetString(1), column, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    private static HashSet<string> ReadTableNames(DbConnection connection, CancellationToken cancellationToken)
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT name FROM sqlite_master WHERE type = 'table';";
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            cancellationToken.ThrowIfCancellationRequested();
            names.Add(reader.GetString(0));
        }
        return names;
    }

    private static HashSet<string> ReadColumnNames(DbConnection connection, string table, CancellationToken cancellationToken)
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using var command = connection.CreateCommand();
        command.CommandText = $"""PRAGMA table_info("{table}");""";
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            cancellationToken.ThrowIfCancellationRequested();
            names.Add(reader.GetString(1));
        }
        return names;
    }

    private static async Task ExecuteAsync(
        DbConnection connection, DbTransaction transaction, string sql, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static void AddParameter(DbCommand command, string name, string value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }
}
