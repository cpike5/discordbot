using DiscordBot.Core.Entities;
using DiscordBot.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace DiscordBot.Bot.Commands;

/// <summary>
/// CLI command to migrate data from a SQLite database to a PostgreSQL database.
/// Reads all entity data from the source SQLite database and writes it to the
/// target PostgreSQL database within a single transaction.
/// </summary>
public static class MigrateDataCommand
{
    private const int BatchSize = 1000;

    private const string Usage = @"
Usage: dotnet run -- migrate-data --source <sqlite-connection-string> --target <postgres-connection-string> [--force]

Options:
  --source  SQLite connection string (e.g., ""Data Source=bot.db"")
  --target  PostgreSQL connection string (e.g., ""Host=localhost;Database=discordbot;Username=postgres;Password=secret"")
  --force   Proceed even if the target database already contains data
  --help    Show this help message

Example:
  dotnet run -- migrate-data --source ""Data Source=./data/bot.db"" --target ""Host=localhost;Database=discordbot;Username=postgres;Password=postgres""
";

    public static async Task<int> ExecuteAsync(string[] args)
    {
        if (args.Any(a => a == "--help" || a == "-h"))
        {
            Console.WriteLine(Usage);
            return 0;
        }

        var source = GetArgValue(args, "--source");
        var target = GetArgValue(args, "--target");

        if (string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(target))
        {
            Console.Error.WriteLine("Error: Both --source and --target arguments are required.");
            Console.WriteLine(Usage);
            return 1;
        }

        Console.WriteLine("=== SQLite to PostgreSQL Data Migration ===");
        Console.WriteLine();

        try
        {
            // Create source (SQLite) context
            var sourceOptions = new DbContextOptionsBuilder<SqliteBotDbContext>()
                .UseSqlite(source)
                .Options;
            using var sourceDb = new SqliteBotDbContext(sourceOptions);

            // Create target (PostgreSQL) context
            var targetOptions = new DbContextOptionsBuilder<PostgresBotDbContext>()
                .UseNpgsql(target)
                .Options;
            using var targetDb = new PostgresBotDbContext(targetOptions);

            // Pre-flight: Verify source SQLite is at latest migration
            Console.WriteLine("[Pre-flight] Verifying source SQLite database...");
            var pendingMigrations = await sourceDb.Database.GetPendingMigrationsAsync();
            var pendingList = pendingMigrations.ToList();
            if (pendingList.Count > 0)
            {
                Console.Error.WriteLine($"Error: Source SQLite database has {pendingList.Count} pending migration(s):");
                foreach (var migration in pendingList)
                {
                    Console.Error.WriteLine($"  - {migration}");
                }
                Console.Error.WriteLine("Please apply all migrations to the source database before migrating data.");
                return 1;
            }
            Console.WriteLine("[Pre-flight] Source database is at latest migration.");

            // Pre-flight: Run MigrateAsync on target PostgreSQL to create/update schema
            Console.WriteLine("[Pre-flight] Applying migrations to target PostgreSQL database...");
            await targetDb.Database.MigrateAsync();
            Console.WriteLine("[Pre-flight] Target database schema is up to date.");

            // Check if target database already has data
            var existingGuildCount = await targetDb.Set<Guild>().CountAsync();
            if (existingGuildCount > 0)
            {
                var forceFlag = args.Any(a => a.Equals("--force", StringComparison.OrdinalIgnoreCase));
                if (!forceFlag)
                {
                    Console.Error.WriteLine("Error: Target database already contains data. Use --force to proceed (WARNING: this will not clear existing data, duplicates may cause errors).");
                    return 1;
                }

                Console.WriteLine("[Pre-flight] WARNING: Target database contains existing data. Proceeding due to --force flag.");
            }

            Console.WriteLine();
            Console.WriteLine("Starting data transfer...");
            Console.WriteLine();

            // Begin single transaction on target
            await using var transaction = await targetDb.Database.BeginTransactionAsync();

            try
            {
                var totalRows = 0;

                // === Tables referenced by Identity ===
                totalRows += await MigrateTableAsync<Theme>(sourceDb, targetDb, "Themes");

                // === Identity tables (parents first) ===
                totalRows += await MigrateTableAsync<IdentityRole>(sourceDb, targetDb, "AspNetRoles");
                totalRows += await MigrateTableAsync<ApplicationUser>(sourceDb, targetDb, "AspNetUsers");
                totalRows += await MigrateTableAsync<IdentityUserRole<string>>(sourceDb, targetDb, "AspNetUserRoles");
                totalRows += await MigrateTableAsync<IdentityUserClaim<string>>(sourceDb, targetDb, "AspNetUserClaims");
                totalRows += await MigrateTableAsync<IdentityUserLogin<string>>(sourceDb, targetDb, "AspNetUserLogins");
                totalRows += await MigrateTableAsync<IdentityUserToken<string>>(sourceDb, targetDb, "AspNetUserTokens");
                totalRows += await MigrateTableAsync<IdentityRoleClaim<string>>(sourceDb, targetDb, "AspNetRoleClaims");

                // === Root entities ===
                totalRows += await MigrateTableAsync<Guild>(sourceDb, targetDb, "Guilds");
                totalRows += await MigrateTableAsync<User>(sourceDb, targetDb, "Users");

                // === Settings tables (FK to Guild) ===
                totalRows += await MigrateTableAsync<ApplicationSetting>(sourceDb, targetDb, "ApplicationSettings");
                totalRows += await MigrateTableAsync<GuildAudioSettings>(sourceDb, targetDb, "GuildAudioSettings");
                totalRows += await MigrateTableAsync<GuildModerationConfig>(sourceDb, targetDb, "GuildModerationConfigs");
                totalRows += await MigrateTableAsync<GuildRatWatchSettings>(sourceDb, targetDb, "GuildRatWatchSettings");
                totalRows += await MigrateTableAsync<GuildTtsSettings>(sourceDb, targetDb, "GuildTtsSettings");
                totalRows += await MigrateTableAsync<CommandModuleConfiguration>(sourceDb, targetDb, "CommandModuleConfigurations");

                // === User-related tables ===
                totalRows += await MigrateTableAsync<UserConsent>(sourceDb, targetDb, "UserConsents");
                totalRows += await MigrateTableAsync<UserActivityLog>(sourceDb, targetDb, "UserActivityLogs");
                totalRows += await MigrateTableAsync<UserGuildAccess>(sourceDb, targetDb, "UserGuildAccess");
                totalRows += await MigrateTableAsync<UserDiscordGuild>(sourceDb, targetDb, "UserDiscordGuilds");
                totalRows += await MigrateTableAsync<DiscordOAuthToken>(sourceDb, targetDb, "DiscordOAuthTokens");

                // === Entities with Guild/User FK ===
                totalRows += await MigrateTableAsync<GuildMember>(sourceDb, targetDb, "GuildMembers");
                totalRows += await MigrateTableAsync<WelcomeConfiguration>(sourceDb, targetDb, "WelcomeConfigurations");
                totalRows += await MigrateTableAsync<VerificationCode>(sourceDb, targetDb, "VerificationCodes");

                // === Content tables ===
                totalRows += await MigrateTableAsync<CommandLog>(sourceDb, targetDb, "CommandLogs");
                totalRows += await MigrateTableAsync<MessageLog>(sourceDb, targetDb, "MessageLogs");
                totalRows += await MigrateTableAsync<AuditLog>(sourceDb, targetDb, "AuditLogs");
                totalRows += await MigrateTableAsync<ScheduledMessage>(sourceDb, targetDb, "ScheduledMessages");
                totalRows += await MigrateTableAsync<Reminder>(sourceDb, targetDb, "Reminders");
                totalRows += await MigrateTableAsync<Sound>(sourceDb, targetDb, "Sounds");
                totalRows += await MigrateTableAsync<ModTag>(sourceDb, targetDb, "ModTags");

                // === Child tables ===
                totalRows += await MigrateTableAsync<SoundPlayLog>(sourceDb, targetDb, "SoundPlayLogs");
                totalRows += await MigrateTableAsync<TtsMessage>(sourceDb, targetDb, "TtsMessages");
                totalRows += await MigrateTableAsync<RatWatch>(sourceDb, targetDb, "RatWatches");
                totalRows += await MigrateTableAsync<ModerationCase>(sourceDb, targetDb, "ModerationCases");
                totalRows += await MigrateTableAsync<FlaggedEvent>(sourceDb, targetDb, "FlaggedEvents");
                totalRows += await MigrateTableAsync<UserNotification>(sourceDb, targetDb, "UserNotifications");
                totalRows += await MigrateTableAsync<CommandRoleRestriction>(sourceDb, targetDb, "CommandRoleRestrictions");

                // === Deeper children ===
                totalRows += await MigrateTableAsync<RatVote>(sourceDb, targetDb, "RatVotes");
                totalRows += await MigrateTableAsync<RatRecord>(sourceDb, targetDb, "RatRecords");
                totalRows += await MigrateTableAsync<ModNote>(sourceDb, targetDb, "ModNotes");
                totalRows += await MigrateTableAsync<UserModTag>(sourceDb, targetDb, "UserModTags");
                totalRows += await MigrateTableAsync<PerformanceAlertConfig>(sourceDb, targetDb, "PerformanceAlertConfigs");
                totalRows += await MigrateTableAsync<PerformanceIncident>(sourceDb, targetDb, "PerformanceIncidents");

                // === Metrics/analytics ===
                totalRows += await MigrateTableAsync<MetricSnapshot>(sourceDb, targetDb, "MetricSnapshots");
                totalRows += await MigrateTableAsync<MemberActivitySnapshot>(sourceDb, targetDb, "MemberActivitySnapshots");
                totalRows += await MigrateTableAsync<ChannelActivitySnapshot>(sourceDb, targetDb, "ChannelActivitySnapshots");
                totalRows += await MigrateTableAsync<GuildMetricsSnapshot>(sourceDb, targetDb, "GuildMetricsSnapshots");
                totalRows += await MigrateTableAsync<ConnectionEvent>(sourceDb, targetDb, "ConnectionEvents");
                totalRows += await MigrateTableAsync<UserActivityEvent>(sourceDb, targetDb, "UserActivityEvents");
                totalRows += await MigrateTableAsync<AssistantGuildSettings>(sourceDb, targetDb, "AssistantGuildSettings");
                totalRows += await MigrateTableAsync<AssistantInteractionLog>(sourceDb, targetDb, "AssistantInteractionLogs");
                totalRows += await MigrateTableAsync<AssistantUsageMetrics>(sourceDb, targetDb, "AssistantUsageMetrics");
                totalRows += await MigrateTableAsync<Watchlist>(sourceDb, targetDb, "Watchlists");

                // Reset PostgreSQL sequences for tables with integer/long auto-increment PKs
                Console.WriteLine();
                Console.WriteLine("Resetting PostgreSQL sequences...");

                // Tables with int Id
                await ResetSequenceAsync(targetDb, "Themes", "Id");
                await ResetSequenceAsync(targetDb, "UserConsents", "Id");
                await ResetSequenceAsync(targetDb, "CommandRoleRestrictions", "Id");
                await ResetSequenceAsync(targetDb, "PerformanceAlertConfigs", "Id");

                // Tables with long Id
                await ResetSequenceAsync(targetDb, "AuditLogs", "Id");
                await ResetSequenceAsync(targetDb, "MessageLogs", "Id");
                await ResetSequenceAsync(targetDb, "SoundPlayLogs", "Id");
                await ResetSequenceAsync(targetDb, "MemberActivitySnapshots", "Id");
                await ResetSequenceAsync(targetDb, "ChannelActivitySnapshots", "Id");
                await ResetSequenceAsync(targetDb, "GuildMetricsSnapshots", "Id");
                await ResetSequenceAsync(targetDb, "MetricSnapshots", "Id");
                await ResetSequenceAsync(targetDb, "UserActivityEvents", "Id");
                await ResetSequenceAsync(targetDb, "ConnectionEvents", "Id");
                await ResetSequenceAsync(targetDb, "AssistantUsageMetrics", "Id");
                await ResetSequenceAsync(targetDb, "AssistantInteractionLogs", "Id");

                // Identity tables with int Id (AspNetUserClaims, AspNetRoleClaims)
                await ResetSequenceAsync(targetDb, "AspNetUserClaims", "Id");
                await ResetSequenceAsync(targetDb, "AspNetRoleClaims", "Id");

                Console.WriteLine("Sequences reset successfully.");

                // Commit transaction
                await transaction.CommitAsync();

                Console.WriteLine();
                Console.WriteLine($"=== Migration complete: {totalRows} total rows transferred ===");
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine();
                Console.Error.WriteLine($"Error during data transfer: {ex.Message}");
                Console.Error.WriteLine("Rolling back transaction...");

                try
                {
                    await transaction.RollbackAsync();
                    Console.Error.WriteLine("Transaction rolled back successfully.");
                }
                catch (Exception rollbackEx)
                {
                    Console.Error.WriteLine($"Error during rollback: {rollbackEx.Message}");
                }

                return 1;
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Fatal error: {ex.Message}");
            if (ex.InnerException != null)
            {
                Console.Error.WriteLine($"Inner exception: {ex.InnerException.Message}");
            }
            return 1;
        }
    }

    private static async Task<int> MigrateTableAsync<TEntity>(
        BotDbContext source,
        BotDbContext target,
        string tableName) where TEntity : class
    {
        Console.Write($"Migrating {tableName}...");

        var totalCount = await source.Set<TEntity>().AsNoTracking().CountAsync();

        if (totalCount == 0)
        {
            Console.WriteLine(" 0 rows transferred");
            return 0;
        }

        var transferred = 0;
        while (transferred < totalCount)
        {
            var batch = await source.Set<TEntity>()
                .AsNoTracking()
                .Skip(transferred)
                .Take(BatchSize)
                .ToListAsync();

            if (batch.Count == 0)
                break;

            await target.Set<TEntity>().AddRangeAsync(batch);
            await target.SaveChangesAsync();
            target.ChangeTracker.Clear();

            transferred += batch.Count;
        }

        Console.WriteLine($" {transferred} rows transferred");
        return transferred;
    }

    private static async Task ResetSequenceAsync(
        BotDbContext target,
        string tableName,
        string idColumn)
    {
        try
        {
            // Use setval(seq, 1, false) for empty tables — 'false' means next nextval() returns 1.
            // For non-empty tables, setval(seq, max) means next nextval() returns max+1.
            await target.Database.ExecuteSqlRawAsync(
                $@"DO $$
                DECLARE seq_name text;
                DECLARE max_val bigint;
                BEGIN
                    seq_name := pg_get_serial_sequence('""{tableName}""', '{idColumn}');
                    IF seq_name IS NULL THEN RETURN; END IF;
                    SELECT MAX(""{idColumn}"") INTO max_val FROM ""{tableName}"";
                    IF max_val IS NULL THEN
                        PERFORM setval(seq_name, 1, false);
                    ELSE
                        PERFORM setval(seq_name, max_val);
                    END IF;
                END $$;");
        }
        catch (Exception ex)
        {
            // Tables without sequences (e.g., non-serial PKs) — log and continue
            Console.WriteLine($"  Warning: Could not reset sequence for {tableName}.{idColumn}: {ex.Message}");
        }
    }

    private static string? GetArgValue(string[] args, string name)
    {
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (args[i].Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                return args[i + 1];
            }
        }
        return null;
    }
}
