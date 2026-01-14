using System.IO.Compression;
using System.Text;
using System.Text.Json;
using DiscordBot.Bot.Tracing;
using DiscordBot.Core.Configuration;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using DiscordBot.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace DiscordBot.Bot.Services;

/// <summary>
/// Service for exporting user data to downloadable files (GDPR Article 15 - Right of Access).
/// </summary>
public class UserDataExportService : IUserDataExportService
{
    private readonly BotDbContext _dbContext;
    private readonly IAuditLogService _auditLogService;
    private readonly IWebHostEnvironment _environment;
    private readonly ApplicationOptions _applicationOptions;
    private readonly ILogger<UserDataExportService> _logger;

    private const int ExportExpirationDays = 7;
    private const string ExportsDirectory = "exports";

    public UserDataExportService(
        BotDbContext dbContext,
        IAuditLogService auditLogService,
        IWebHostEnvironment environment,
        IOptions<ApplicationOptions> applicationOptions,
        ILogger<UserDataExportService> logger)
    {
        _dbContext = dbContext;
        _auditLogService = auditLogService;
        _environment = environment;
        _applicationOptions = applicationOptions.Value;
        _logger = logger;
    }

    public async Task<UserDataExportResultDto> ExportUserDataAsync(
        ulong discordUserId,
        CancellationToken cancellationToken = default)
    {
        using var activity = BotActivitySource.StartServiceActivity(
            "userdataexport",
            "export_user_data",
            userId: discordUserId);

        try
        {
            _logger.LogInformation(
                "Starting user data export for Discord user {DiscordUserId}",
                discordUserId);

            // Check if user exists
            var userExists = await _dbContext.Users.AnyAsync(
                u => u.Id == discordUserId, cancellationToken);

            if (!userExists)
            {
                _logger.LogWarning("Discord user {DiscordUserId} not found in database", discordUserId);
                activity?.SetTag("export.user_found", false);

                return UserDataExportResultDto.Failed(
                    UserDataExportResultDto.UserNotFound,
                    "User not found in the database");
            }

            activity?.SetTag("export.user_found", true);

            var exportId = Guid.NewGuid();
            var exportedCounts = new Dictionary<string, int>();
            var correlationId = Guid.NewGuid().ToString();

            // Create export directory structure
            var userExportPath = Path.Combine(
                _environment.WebRootPath,
                ExportsDirectory,
                discordUserId.ToString());

            var tempExportPath = Path.Combine(
                Path.GetTempPath(),
                $"export_{exportId}");

            try
            {
                Directory.CreateDirectory(userExportPath);
                Directory.CreateDirectory(tempExportPath);

                // Export all data categories to JSON files
                await ExportMessageLogsAsync(discordUserId, tempExportPath, exportedCounts, cancellationToken);
                await ExportCommandLogsAsync(discordUserId, tempExportPath, exportedCounts, cancellationToken);
                await ExportRatVotesAsync(discordUserId, tempExportPath, exportedCounts, cancellationToken);
                await ExportRatRecordsAsync(discordUserId, tempExportPath, exportedCounts, cancellationToken);
                await ExportRatWatchesAsync(discordUserId, tempExportPath, exportedCounts, cancellationToken);
                await ExportRemindersAsync(discordUserId, tempExportPath, exportedCounts, cancellationToken);
                await ExportModNotesAsync(discordUserId, tempExportPath, exportedCounts, cancellationToken);
                await ExportUserModTagsAsync(discordUserId, tempExportPath, exportedCounts, cancellationToken);
                await ExportWatchlistsAsync(discordUserId, tempExportPath, exportedCounts, cancellationToken);
                await ExportSoundPlayLogsAsync(discordUserId, tempExportPath, exportedCounts, cancellationToken);
                await ExportTtsMessagesAsync(discordUserId, tempExportPath, exportedCounts, cancellationToken);
                await ExportGuildMembersAsync(discordUserId, tempExportPath, exportedCounts, cancellationToken);
                await ExportUserConsentsAsync(discordUserId, tempExportPath, exportedCounts, cancellationToken);
                await ExportUserProfileAsync(discordUserId, tempExportPath, exportedCounts, cancellationToken);
                await ExportApplicationUserAsync(discordUserId, tempExportPath, exportedCounts, cancellationToken);

                // Create README with export info
                await CreateReadmeFileAsync(discordUserId, exportId, tempExportPath, exportedCounts);

                // Create ZIP archive
                var zipFileName = $"{exportId}.zip";
                var zipFilePath = Path.Combine(userExportPath, zipFileName);

                ZipFile.CreateFromDirectory(tempExportPath, zipFilePath, CompressionLevel.Optimal, false);

                // Clean up temp directory
                Directory.Delete(tempExportPath, recursive: true);

                var expiresAt = DateTime.UtcNow.AddDays(ExportExpirationDays);
                var downloadUrl = $"{_applicationOptions.BaseUrl.TrimEnd('/')}/exports/{discordUserId}/{zipFileName}";

                _logger.LogInformation(
                    "Successfully exported data for Discord user {DiscordUserId}. Export ID: {ExportId}, Total records: {TotalRecords}",
                    discordUserId, exportId, exportedCounts.Values.Sum());

                // Create audit log entry
                try
                {
                    _auditLogService.CreateBuilder()
                        .ForCategory(AuditLogCategory.User)
                        .WithAction(AuditLogAction.UserDataExported)
                        .ByUser(discordUserId.ToString())
                        .OnTarget("User", discordUserId.ToString())
                        .WithDetails(new
                        {
                            exportId,
                            exportedCounts,
                            expiresAt,
                            timestamp = DateTime.UtcNow
                        })
                        .WithCorrelationId(correlationId)
                        .Enqueue();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to create audit log entry for user data export");
                }

                activity?.SetTag("export.success", true);
                activity?.SetTag("export.total_records", exportedCounts.Values.Sum());
                BotActivitySource.SetSuccess(activity);

                return UserDataExportResultDto.Succeeded(downloadUrl, expiresAt, exportId, exportedCounts);
            }
            catch (Exception ex)
            {
                // Clean up on error
                if (Directory.Exists(tempExportPath))
                {
                    Directory.Delete(tempExportPath, recursive: true);
                }

                _logger.LogError(ex, "Failed to create export files for Discord user {DiscordUserId}", discordUserId);

                activity?.SetTag("export.success", false);
                BotActivitySource.RecordException(activity, ex);

                return UserDataExportResultDto.Failed(
                    UserDataExportResultDto.FileSystemError,
                    "Failed to create export files: " + ex.Message);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Unexpected error while exporting data for Discord user {DiscordUserId}",
                discordUserId);

            BotActivitySource.RecordException(activity, ex);

            return UserDataExportResultDto.Failed(
                UserDataExportResultDto.DatabaseError,
                "An unexpected error occurred while exporting user data");
        }
    }

    public async Task<int> CleanupExpiredExportsAsync(CancellationToken cancellationToken = default)
    {
        using var activity = BotActivitySource.StartServiceActivity(
            "userdataexport",
            "cleanup_expired_exports");

        try
        {
            var exportsBasePath = Path.Combine(_environment.WebRootPath, ExportsDirectory);

            if (!Directory.Exists(exportsBasePath))
            {
                _logger.LogDebug("Exports directory does not exist, nothing to clean up");
                return 0;
            }

            var cleanedCount = 0;
            var expirationDate = DateTime.UtcNow.AddDays(-ExportExpirationDays);

            // Iterate through user directories
            foreach (var userDir in Directory.GetDirectories(exportsBasePath))
            {
                foreach (var zipFile in Directory.GetFiles(userDir, "*.zip"))
                {
                    var fileInfo = new FileInfo(zipFile);

                    if (fileInfo.CreationTimeUtc < expirationDate)
                    {
                        try
                        {
                            File.Delete(zipFile);
                            cleanedCount++;
                            _logger.LogInformation("Deleted expired export file: {FilePath}", zipFile);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Failed to delete expired export file: {FilePath}", zipFile);
                        }
                    }
                }

                // Remove empty user directories
                if (!Directory.EnumerateFileSystemEntries(userDir).Any())
                {
                    try
                    {
                        Directory.Delete(userDir);
                        _logger.LogDebug("Deleted empty user export directory: {DirPath}", userDir);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to delete empty directory: {DirPath}", userDir);
                    }
                }
            }

            _logger.LogInformation("Cleaned up {CleanedCount} expired export files", cleanedCount);

            activity?.SetTag("cleanup.files_deleted", cleanedCount);
            BotActivitySource.SetSuccess(activity);

            return cleanedCount;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during export cleanup");
            BotActivitySource.RecordException(activity, ex);
            return 0;
        }
    }

    private async Task ExportMessageLogsAsync(ulong userId, string exportPath, Dictionary<string, int> counts, CancellationToken ct)
    {
        var data = await _dbContext.MessageLogs
            .Where(m => m.AuthorId == userId)
            .Select(m => new
            {
                m.Id,
                m.DiscordMessageId,
                m.AuthorId,
                m.ChannelId,
                m.ChannelName,
                m.GuildId,
                Source = m.Source.ToString(),
                m.Content,
                m.Timestamp,
                m.LoggedAt,
                m.HasAttachments,
                m.HasEmbeds,
                m.ReplyToMessageId
            })
            .ToListAsync(ct);

        counts["MessageLogs"] = data.Count;
        if (data.Count > 0)
        {
            await WriteJsonFileAsync(exportPath, "message_logs.json", data);
        }
    }

    private async Task ExportCommandLogsAsync(ulong userId, string exportPath, Dictionary<string, int> counts, CancellationToken ct)
    {
        var data = await _dbContext.CommandLogs
            .Where(c => c.UserId == userId)
            .Select(c => new
            {
                c.Id,
                c.GuildId,
                c.UserId,
                c.CommandName,
                c.Parameters,
                c.ExecutedAt,
                c.ResponseTimeMs,
                c.Success,
                c.ErrorMessage,
                c.CorrelationId
            })
            .ToListAsync(ct);

        counts["CommandLogs"] = data.Count;
        if (data.Count > 0)
        {
            await WriteJsonFileAsync(exportPath, "command_logs.json", data);
        }
    }

    private async Task ExportRatVotesAsync(ulong userId, string exportPath, Dictionary<string, int> counts, CancellationToken ct)
    {
        var data = await _dbContext.RatVotes
            .Where(v => v.VoterUserId == userId)
            .Select(v => new
            {
                v.Id,
                v.RatWatchId,
                v.VoterUserId,
                v.IsGuiltyVote,
                v.VotedAt
            })
            .ToListAsync(ct);

        counts["RatVotes"] = data.Count;
        if (data.Count > 0)
        {
            await WriteJsonFileAsync(exportPath, "rat_votes.json", data);
        }
    }

    private async Task ExportRatRecordsAsync(ulong userId, string exportPath, Dictionary<string, int> counts, CancellationToken ct)
    {
        var data = await _dbContext.RatRecords
            .Where(r => r.UserId == userId)
            .Select(r => new
            {
                r.Id,
                r.RatWatchId,
                r.GuildId,
                r.UserId,
                r.GuiltyVotes,
                r.NotGuiltyVotes,
                r.RecordedAt,
                r.OriginalMessageLink
            })
            .ToListAsync(ct);

        counts["RatRecords"] = data.Count;
        if (data.Count > 0)
        {
            await WriteJsonFileAsync(exportPath, "rat_records.json", data);
        }
    }

    private async Task ExportRatWatchesAsync(ulong userId, string exportPath, Dictionary<string, int> counts, CancellationToken ct)
    {
        var data = await _dbContext.RatWatches
            .Where(w => w.AccusedUserId == userId || w.InitiatorUserId == userId)
            .Select(w => new
            {
                w.Id,
                w.GuildId,
                w.ChannelId,
                w.AccusedUserId,
                w.InitiatorUserId,
                w.CustomMessage,
                w.ScheduledAt,
                w.CreatedAt,
                Status = w.Status.ToString(),
                w.ClearedAt,
                w.VotingStartedAt,
                w.VotingEndedAt
            })
            .ToListAsync(ct);

        counts["RatWatches"] = data.Count;
        if (data.Count > 0)
        {
            await WriteJsonFileAsync(exportPath, "rat_watches.json", data);
        }
    }

    private async Task ExportRemindersAsync(ulong userId, string exportPath, Dictionary<string, int> counts, CancellationToken ct)
    {
        var data = await _dbContext.Reminders
            .Where(r => r.UserId == userId)
            .Select(r => new
            {
                r.Id,
                r.UserId,
                r.GuildId,
                r.ChannelId,
                r.Message,
                r.CreatedAt,
                r.TriggerAt,
                r.DeliveredAt,
                Status = r.Status.ToString(),
                r.DeliveryAttempts,
                r.LastError
            })
            .ToListAsync(ct);

        counts["Reminders"] = data.Count;
        if (data.Count > 0)
        {
            await WriteJsonFileAsync(exportPath, "reminders.json", data);
        }
    }

    private async Task ExportModNotesAsync(ulong userId, string exportPath, Dictionary<string, int> counts, CancellationToken ct)
    {
        var data = await _dbContext.ModNotes
            .Where(n => n.AuthorUserId == userId)
            .Select(n => new
            {
                n.Id,
                n.GuildId,
                n.TargetUserId,
                n.AuthorUserId,
                n.Content,
                n.CreatedAt
            })
            .ToListAsync(ct);

        counts["ModNotes"] = data.Count;
        if (data.Count > 0)
        {
            await WriteJsonFileAsync(exportPath, "mod_notes.json", data);
        }
    }

    private async Task ExportUserModTagsAsync(ulong userId, string exportPath, Dictionary<string, int> counts, CancellationToken ct)
    {
        var data = await _dbContext.UserModTags
            .Where(t => t.UserId == userId)
            .Select(t => new
            {
                t.Id,
                t.GuildId,
                t.UserId,
                t.TagId,
                t.AppliedByUserId,
                t.AppliedAt
            })
            .ToListAsync(ct);

        counts["UserModTags"] = data.Count;
        if (data.Count > 0)
        {
            await WriteJsonFileAsync(exportPath, "mod_tags.json", data);
        }
    }

    private async Task ExportWatchlistsAsync(ulong userId, string exportPath, Dictionary<string, int> counts, CancellationToken ct)
    {
        var data = await _dbContext.Watchlists
            .Where(w => w.UserId == userId)
            .Select(w => new
            {
                w.Id,
                w.GuildId,
                w.UserId,
                w.AddedByUserId,
                w.Reason,
                w.AddedAt
            })
            .ToListAsync(ct);

        counts["Watchlists"] = data.Count;
        if (data.Count > 0)
        {
            await WriteJsonFileAsync(exportPath, "watchlists.json", data);
        }
    }

    private async Task ExportSoundPlayLogsAsync(ulong userId, string exportPath, Dictionary<string, int> counts, CancellationToken ct)
    {
        var data = await _dbContext.SoundPlayLogs
            .Where(s => s.UserId == userId)
            .Select(s => new
            {
                s.Id,
                s.GuildId,
                s.UserId,
                s.SoundId,
                s.PlayedAt
            })
            .ToListAsync(ct);

        counts["SoundPlayLogs"] = data.Count;
        if (data.Count > 0)
        {
            await WriteJsonFileAsync(exportPath, "sound_play_logs.json", data);
        }
    }

    private async Task ExportTtsMessagesAsync(ulong userId, string exportPath, Dictionary<string, int> counts, CancellationToken ct)
    {
        var data = await _dbContext.TtsMessages
            .Where(t => t.UserId == userId)
            .Select(t => new
            {
                t.Id,
                t.GuildId,
                t.UserId,
                t.Username,
                t.Message,
                t.Voice,
                t.DurationSeconds,
                t.CreatedAt
            })
            .ToListAsync(ct);

        counts["TtsMessages"] = data.Count;
        if (data.Count > 0)
        {
            await WriteJsonFileAsync(exportPath, "tts_messages.json", data);
        }
    }

    private async Task ExportGuildMembersAsync(ulong userId, string exportPath, Dictionary<string, int> counts, CancellationToken ct)
    {
        var data = await _dbContext.GuildMembers
            .Where(g => g.UserId == userId)
            .Select(g => new
            {
                g.GuildId,
                g.UserId,
                g.Nickname,
                g.JoinedAt,
                g.LastActiveAt,
                g.IsActive
            })
            .ToListAsync(ct);

        counts["GuildMembers"] = data.Count;
        if (data.Count > 0)
        {
            await WriteJsonFileAsync(exportPath, "guild_members.json", data);
        }
    }

    private async Task ExportUserConsentsAsync(ulong userId, string exportPath, Dictionary<string, int> counts, CancellationToken ct)
    {
        var data = await _dbContext.UserConsents
            .Where(c => c.DiscordUserId == userId)
            .Select(c => new
            {
                c.Id,
                c.DiscordUserId,
                ConsentType = c.ConsentType.ToString(),
                c.GrantedAt,
                c.RevokedAt,
                c.GrantedVia,
                c.RevokedVia
            })
            .ToListAsync(ct);

        counts["UserConsents"] = data.Count;
        if (data.Count > 0)
        {
            await WriteJsonFileAsync(exportPath, "consents.json", data);
        }
    }

    private async Task ExportUserProfileAsync(ulong userId, string exportPath, Dictionary<string, int> counts, CancellationToken ct)
    {
        var data = await _dbContext.Users
            .Where(u => u.Id == userId)
            .Select(u => new
            {
                u.Id,
                u.Username,
                u.Discriminator,
                u.GlobalDisplayName,
                u.AvatarHash,
                u.FirstSeenAt,
                u.LastSeenAt,
                u.AccountCreatedAt
            })
            .ToListAsync(ct);

        counts["Users"] = data.Count;
        if (data.Count > 0)
        {
            await WriteJsonFileAsync(exportPath, "user_profile.json", data);
        }
    }

    private async Task ExportApplicationUserAsync(ulong userId, string exportPath, Dictionary<string, int> counts, CancellationToken ct)
    {
        var applicationUser = await _dbContext.Users
            .OfType<Core.Entities.ApplicationUser>()
            .Where(u => u.DiscordUserId == userId)
            .Select(u => new
            {
                u.Id,
                u.UserName,
                u.Email,
                u.DiscordUserId,
                u.DiscordUsername,
                u.EmailConfirmed,
                u.CreatedAt,
                u.LastLoginAt
            })
            .FirstOrDefaultAsync(ct);

        if (applicationUser != null)
        {
            counts["ApplicationUser"] = 1;

            // Get guild access
            var guildAccess = await _dbContext.UserGuildAccess
                .Where(uga => uga.ApplicationUserId == applicationUser.Id)
                .Select(uga => new
                {
                    uga.GuildId,
                    uga.AccessLevel,
                    uga.GrantedAt,
                    uga.GrantedByUserId
                })
                .ToListAsync(ct);

            counts["UserGuildAccess"] = guildAccess.Count;

            // Get Discord guilds
            var discordGuilds = await _dbContext.UserDiscordGuilds
                .Where(udg => udg.ApplicationUserId == applicationUser.Id)
                .Select(udg => new
                {
                    udg.GuildId,
                    udg.GuildName,
                    udg.GuildIconHash,
                    udg.IsOwner,
                    udg.Permissions,
                    udg.CapturedAt,
                    udg.LastUpdatedAt
                })
                .ToListAsync(ct);

            counts["UserDiscordGuilds"] = discordGuilds.Count;

            var exportData = new
            {
                ApplicationUser = applicationUser,
                GuildAccess = guildAccess,
                DiscordGuilds = discordGuilds
            };

            await WriteJsonFileAsync(exportPath, "application_user.json", exportData);
        }
        else
        {
            counts["ApplicationUser"] = 0;
            counts["UserGuildAccess"] = 0;
            counts["UserDiscordGuilds"] = 0;
        }
    }

    private async Task WriteJsonFileAsync(string exportPath, string fileName, object data)
    {
        var filePath = Path.Combine(exportPath, fileName);
        var json = JsonSerializer.Serialize(data, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        await File.WriteAllTextAsync(filePath, json, Encoding.UTF8);
        _logger.LogDebug("Wrote export file: {FileName}", fileName);
    }

    private async Task CreateReadmeFileAsync(
        ulong userId,
        Guid exportId,
        string exportPath,
        Dictionary<string, int> counts)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# User Data Export");
        sb.AppendLine();
        sb.AppendLine($"**Export ID:** {exportId}");
        sb.AppendLine($"**Discord User ID:** {userId}");
        sb.AppendLine($"**Exported At:** {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
        sb.AppendLine($"**Expires At:** {DateTime.UtcNow.AddDays(ExportExpirationDays):yyyy-MM-dd HH:mm:ss} UTC");
        sb.AppendLine();
        sb.AppendLine("## Data Summary");
        sb.AppendLine();

        foreach (var (category, count) in counts.OrderBy(kvp => kvp.Key))
        {
            sb.AppendLine($"- **{GetFriendlyName(category)}:** {count} records");
        }

        sb.AppendLine();
        sb.AppendLine("## Files");
        sb.AppendLine();
        sb.AppendLine("This export contains the following JSON files:");
        sb.AppendLine();
        sb.AppendLine("- `README.txt` - This file");
        sb.AppendLine("- `user_profile.json` - Your basic user profile information");
        sb.AppendLine("- `message_logs.json` - Messages you've sent (if any)");
        sb.AppendLine("- `command_logs.json` - Commands you've executed (if any)");
        sb.AppendLine("- `rat_votes.json` - Rat Watch votes you've cast (if any)");
        sb.AppendLine("- `rat_records.json` - Rat Watch records associated with you (if any)");
        sb.AppendLine("- `rat_watches.json` - Rat Watch incidents you're involved in (if any)");
        sb.AppendLine("- `reminders.json` - Your reminders (if any)");
        sb.AppendLine("- `mod_notes.json` - Moderation notes you've authored (if any)");
        sb.AppendLine("- `mod_tags.json` - Moderation tags applied to you (if any)");
        sb.AppendLine("- `watchlists.json` - Watchlist entries for you (if any)");
        sb.AppendLine("- `sound_play_logs.json` - Soundboard usage history (if any)");
        sb.AppendLine("- `tts_messages.json` - Text-to-speech messages (if any)");
        sb.AppendLine("- `guild_members.json` - Your guild membership information (if any)");
        sb.AppendLine("- `consents.json` - Your consent preferences (if any)");
        sb.AppendLine("- `application_user.json` - Your admin account data (if linked)");
        sb.AppendLine();
        sb.AppendLine("## Data Format");
        sb.AppendLine();
        sb.AppendLine("All files are in JSON format and can be opened with any text editor or JSON viewer.");
        sb.AppendLine();
        sb.AppendLine("## Privacy Notice");
        sb.AppendLine();
        sb.AppendLine("This export was created in compliance with GDPR Article 15 (Right of Access).");
        sb.AppendLine("The export file will be automatically deleted after 7 days.");
        sb.AppendLine();
        sb.AppendLine("If you have any questions or concerns about your data, please contact the bot administrator.");

        var readmePath = Path.Combine(exportPath, "README.txt");
        await File.WriteAllTextAsync(readmePath, sb.ToString(), Encoding.UTF8);
    }

    private static string GetFriendlyName(string category)
    {
        return category switch
        {
            "MessageLogs" => "Message Logs",
            "CommandLogs" => "Command Logs",
            "RatVotes" => "Rat Watch Votes",
            "RatRecords" => "Rat Watch Records",
            "RatWatches" => "Rat Watches",
            "Reminders" => "Reminders",
            "ModNotes" => "Moderation Notes",
            "UserModTags" => "Moderation Tags",
            "Watchlists" => "Watchlist Entries",
            "SoundPlayLogs" => "Soundboard History",
            "TtsMessages" => "TTS Messages",
            "GuildMembers" => "Guild Memberships",
            "UserConsents" => "Consent Records",
            "Users" => "User Profile",
            "ApplicationUser" => "Admin Account",
            "UserGuildAccess" => "Guild Access Permissions",
            "UserDiscordGuilds" => "Discord Guild Links",
            _ => category
        };
    }
}
