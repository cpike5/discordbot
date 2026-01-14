using DiscordBot.Bot.Tracing;
using DiscordBot.Core.Authorization;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using DiscordBot.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace DiscordBot.Bot.Services;

/// <summary>
/// Service for purging user data from the system (GDPR right to be forgotten).
/// </summary>
public class UserPurgeService : IUserPurgeService
{
    private readonly BotDbContext _dbContext;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IAuditLogService _auditLogService;
    private readonly IMemoryCache _cache;
    private readonly ILogger<UserPurgeService> _logger;

    public UserPurgeService(
        BotDbContext dbContext,
        UserManager<ApplicationUser> userManager,
        IAuditLogService auditLogService,
        IMemoryCache cache,
        ILogger<UserPurgeService> logger)
    {
        _dbContext = dbContext;
        _userManager = userManager;
        _auditLogService = auditLogService;
        _cache = cache;
        _logger = logger;
    }

    public async Task<UserPurgeResultDto> PurgeUserDataAsync(
        ulong discordUserId,
        PurgeInitiator initiator,
        string? initiatorId = null,
        CancellationToken cancellationToken = default)
    {
        using var activity = BotActivitySource.StartServiceActivity(
            "userpurge",
            "purge_user_data",
            userId: discordUserId);

        try
        {
            activity?.SetTag("purge.initiator", initiator.ToString());
            if (initiatorId != null)
            {
                activity?.SetTag("purge.initiator_id", initiatorId);
            }

            _logger.LogInformation(
                "Starting user data purge for Discord user {DiscordUserId}, initiated by {Initiator}",
                discordUserId, initiator);

            // Check if user can be purged
            var (canPurge, blockingReason) = await CanPurgeUserAsync(discordUserId, cancellationToken);
            if (!canPurge)
            {
                _logger.LogWarning(
                    "Cannot purge Discord user {DiscordUserId}: {BlockingReason}",
                    discordUserId, blockingReason);

                activity?.SetTag("purge.blocked", true);
                activity?.SetTag("purge.blocking_reason", blockingReason);

                return UserPurgeResultDto.Failed(
                    UserPurgeResultDto.UserHasAdminRole,
                    blockingReason ?? "User cannot be purged");
            }

            // Check if user exists
            var userExists = await _dbContext.Users.AnyAsync(
                u => u.Id == discordUserId, cancellationToken);

            if (!userExists)
            {
                _logger.LogWarning("Discord user {DiscordUserId} not found in database", discordUserId);
                activity?.SetTag("purge.user_found", false);

                return UserPurgeResultDto.Failed(
                    UserPurgeResultDto.UserNotFound,
                    "User not found in the database");
            }

            activity?.SetTag("purge.user_found", true);

            // Begin transaction
            await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

            try
            {
                var deletedCounts = new Dictionary<string, int>();
                var correlationId = Guid.NewGuid().ToString();

                // 1. MessageLogs (AuthorId = discordUserId)
                var messageLogs = await _dbContext.MessageLogs
                    .Where(m => m.AuthorId == discordUserId)
                    .ExecuteDeleteAsync(cancellationToken);
                deletedCounts["MessageLogs"] = messageLogs;

                // 2. CommandLogs (UserId = discordUserId)
                var commandLogs = await _dbContext.CommandLogs
                    .Where(c => c.UserId == discordUserId)
                    .ExecuteDeleteAsync(cancellationToken);
                deletedCounts["CommandLogs"] = commandLogs;

                // 3. RatVotes (VoterUserId = discordUserId)
                var ratVotes = await _dbContext.RatVotes
                    .Where(v => v.VoterUserId == discordUserId)
                    .ExecuteDeleteAsync(cancellationToken);
                deletedCounts["RatVotes"] = ratVotes;

                // 4. RatRecords - ANONYMIZE instead of delete (set UserId to 0)
                var ratRecords = await _dbContext.RatRecords
                    .Where(r => r.UserId == discordUserId)
                    .ExecuteUpdateAsync(
                        setters => setters.SetProperty(r => r.UserId, (ulong)0),
                        cancellationToken);
                deletedCounts["RatRecords_Anonymized"] = ratRecords;

                // 5. RatWatches - ANONYMIZE instead of delete (set AccusedUserId and InitiatorUserId to 0)
                var ratWatchesAccused = await _dbContext.RatWatches
                    .Where(w => w.AccusedUserId == discordUserId)
                    .ExecuteUpdateAsync(
                        setters => setters.SetProperty(w => w.AccusedUserId, (ulong)0),
                        cancellationToken);

                var ratWatchesInitiator = await _dbContext.RatWatches
                    .Where(w => w.InitiatorUserId == discordUserId)
                    .ExecuteUpdateAsync(
                        setters => setters.SetProperty(w => w.InitiatorUserId, (ulong)0),
                        cancellationToken);
                deletedCounts["RatWatches_Anonymized"] = ratWatchesAccused + ratWatchesInitiator;

                // 6. Reminders (UserId = discordUserId)
                var reminders = await _dbContext.Reminders
                    .Where(r => r.UserId == discordUserId)
                    .ExecuteDeleteAsync(cancellationToken);
                deletedCounts["Reminders"] = reminders;

                // 7. ModNotes (AuthorUserId = discordUserId)
                var modNotes = await _dbContext.ModNotes
                    .Where(n => n.AuthorUserId == discordUserId)
                    .ExecuteDeleteAsync(cancellationToken);
                deletedCounts["ModNotes"] = modNotes;

                // 8. UserModTags (UserId = discordUserId)
                var userModTags = await _dbContext.UserModTags
                    .Where(t => t.UserId == discordUserId)
                    .ExecuteDeleteAsync(cancellationToken);
                deletedCounts["UserModTags"] = userModTags;

                // 9. Watchlists (UserId = discordUserId)
                var watchlists = await _dbContext.Watchlists
                    .Where(w => w.UserId == discordUserId)
                    .ExecuteDeleteAsync(cancellationToken);
                deletedCounts["Watchlists"] = watchlists;

                // 10. SoundPlayLogs (UserId = discordUserId)
                var soundPlayLogs = await _dbContext.SoundPlayLogs
                    .Where(s => s.UserId == discordUserId)
                    .ExecuteDeleteAsync(cancellationToken);
                deletedCounts["SoundPlayLogs"] = soundPlayLogs;

                // 11. TtsMessages (UserId = discordUserId)
                var ttsMessages = await _dbContext.TtsMessages
                    .Where(t => t.UserId == discordUserId)
                    .ExecuteDeleteAsync(cancellationToken);
                deletedCounts["TtsMessages"] = ttsMessages;

                // 12. GuildMembers (UserId = discordUserId)
                var guildMembers = await _dbContext.GuildMembers
                    .Where(g => g.UserId == discordUserId)
                    .ExecuteDeleteAsync(cancellationToken);
                deletedCounts["GuildMembers"] = guildMembers;

                // 13. UserConsents (DiscordUserId = discordUserId)
                var userConsents = await _dbContext.UserConsents
                    .Where(c => c.DiscordUserId == discordUserId)
                    .ExecuteDeleteAsync(cancellationToken);
                deletedCounts["UserConsents"] = userConsents;

                // 14. Users (the User entity itself)
                var users = await _dbContext.Users
                    .Where(u => u.Id == discordUserId)
                    .ExecuteDeleteAsync(cancellationToken);
                deletedCounts["Users"] = users;

                // 15. ApplicationUser (if linked via DiscordUserId)
                var applicationUser = await _userManager.Users
                    .FirstOrDefaultAsync(u => u.DiscordUserId == discordUserId, cancellationToken);

                if (applicationUser != null)
                {
                    // Delete associated records first
                    var userGuildAccess = await _dbContext.UserGuildAccess
                        .Where(uga => uga.ApplicationUserId == applicationUser.Id)
                        .ExecuteDeleteAsync(cancellationToken);
                    deletedCounts["UserGuildAccess"] = userGuildAccess;

                    var userDiscordGuilds = await _dbContext.UserDiscordGuilds
                        .Where(udg => udg.ApplicationUserId == applicationUser.Id)
                        .ExecuteDeleteAsync(cancellationToken);
                    deletedCounts["UserDiscordGuilds"] = userDiscordGuilds;

                    var discordOAuthTokens = await _dbContext.DiscordOAuthTokens
                        .Where(t => t.ApplicationUserId == applicationUser.Id)
                        .ExecuteDeleteAsync(cancellationToken);
                    deletedCounts["DiscordOAuthTokens"] = discordOAuthTokens;

                    // Delete the ApplicationUser via UserManager (handles roles, claims, etc.)
                    var result = await _userManager.DeleteAsync(applicationUser);
                    if (result.Succeeded)
                    {
                        deletedCounts["ApplicationUser"] = 1;
                    }
                    else
                    {
                        _logger.LogError(
                            "Failed to delete ApplicationUser for Discord user {DiscordUserId}: {Errors}",
                            discordUserId, string.Join(", ", result.Errors.Select(e => e.Description)));
                    }
                }

                // Commit transaction
                await transaction.CommitAsync(cancellationToken);

                _logger.LogInformation(
                    "Successfully purged data for Discord user {DiscordUserId}. Deleted counts: {DeletedCounts}",
                    discordUserId, System.Text.Json.JsonSerializer.Serialize(deletedCounts));

                // Invalidate all cached data for the user
                InvalidateUserCaches(discordUserId);

                // Create audit log entry (anonymized - no PII)
                try
                {
                    var initiatorIdForAudit = initiator switch
                    {
                        PurgeInitiator.User => "[PURGED]",
                        PurgeInitiator.Admin => initiatorId ?? "Unknown",
                        PurgeInitiator.System => "System",
                        _ => "Unknown"
                    };

                    _auditLogService.CreateBuilder()
                        .ForCategory(AuditLogCategory.User)
                        .WithAction(AuditLogAction.UserDataPurged)
                        .ByUser(initiatorIdForAudit)
                        .OnTarget("User", "[PURGED]") // Don't store the Discord user ID
                        .WithDetails(new
                        {
                            initiator = initiator.ToString(),
                            deletedCounts,
                            timestamp = DateTime.UtcNow
                        })
                        .WithCorrelationId(correlationId)
                        .Enqueue();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "Failed to create audit log entry for user data purge (Discord user already purged)");
                }

                activity?.SetTag("purge.success", true);
                activity?.SetTag("purge.total_deleted", deletedCounts.Values.Sum());
                BotActivitySource.SetSuccess(activity);

                return UserPurgeResultDto.Succeeded(deletedCounts, correlationId);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(cancellationToken);

                _logger.LogError(ex,
                    "Transaction failed while purging data for Discord user {DiscordUserId}",
                    discordUserId);

                activity?.SetTag("purge.success", false);
                BotActivitySource.RecordException(activity, ex);

                return UserPurgeResultDto.Failed(
                    UserPurgeResultDto.TransactionFailed,
                    "Failed to purge user data: " + ex.Message);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Unexpected error while purging data for Discord user {DiscordUserId}",
                discordUserId);

            BotActivitySource.RecordException(activity, ex);

            return UserPurgeResultDto.Failed(
                UserPurgeResultDto.DatabaseError,
                "An unexpected error occurred while purging user data");
        }
    }

    public async Task<UserPurgeResultDto> PreviewPurgeAsync(
        ulong discordUserId,
        CancellationToken cancellationToken = default)
    {
        using var activity = BotActivitySource.StartServiceActivity(
            "userpurge",
            "preview_purge",
            userId: discordUserId);

        try
        {
            _logger.LogDebug("Previewing purge for Discord user {DiscordUserId}", discordUserId);

            // Check if user exists
            var userExists = await _dbContext.Users.AnyAsync(
                u => u.Id == discordUserId, cancellationToken);

            if (!userExists)
            {
                _logger.LogDebug("Discord user {DiscordUserId} not found in database", discordUserId);
                activity?.SetTag("preview.user_found", false);

                return UserPurgeResultDto.Failed(
                    UserPurgeResultDto.UserNotFound,
                    "User not found in the database");
            }

            activity?.SetTag("preview.user_found", true);

            // Count records that would be deleted
            var counts = new Dictionary<string, int>
            {
                ["MessageLogs"] = await _dbContext.MessageLogs.CountAsync(m => m.AuthorId == discordUserId, cancellationToken),
                ["CommandLogs"] = await _dbContext.CommandLogs.CountAsync(c => c.UserId == discordUserId, cancellationToken),
                ["RatVotes"] = await _dbContext.RatVotes.CountAsync(v => v.VoterUserId == discordUserId, cancellationToken),
                ["RatRecords_Anonymized"] = await _dbContext.RatRecords.CountAsync(r => r.UserId == discordUserId, cancellationToken),
                ["RatWatches_Anonymized"] = await _dbContext.RatWatches.CountAsync(w => w.AccusedUserId == discordUserId || w.InitiatorUserId == discordUserId, cancellationToken),
                ["Reminders"] = await _dbContext.Reminders.CountAsync(r => r.UserId == discordUserId, cancellationToken),
                ["ModNotes"] = await _dbContext.ModNotes.CountAsync(n => n.AuthorUserId == discordUserId, cancellationToken),
                ["UserModTags"] = await _dbContext.UserModTags.CountAsync(t => t.UserId == discordUserId, cancellationToken),
                ["Watchlists"] = await _dbContext.Watchlists.CountAsync(w => w.UserId == discordUserId, cancellationToken),
                ["SoundPlayLogs"] = await _dbContext.SoundPlayLogs.CountAsync(s => s.UserId == discordUserId, cancellationToken),
                ["TtsMessages"] = await _dbContext.TtsMessages.CountAsync(t => t.UserId == discordUserId, cancellationToken),
                ["GuildMembers"] = await _dbContext.GuildMembers.CountAsync(g => g.UserId == discordUserId, cancellationToken),
                ["UserConsents"] = await _dbContext.UserConsents.CountAsync(c => c.DiscordUserId == discordUserId, cancellationToken),
                ["Users"] = await _dbContext.Users.CountAsync(u => u.Id == discordUserId, cancellationToken)
            };

            // Check for linked ApplicationUser
            var applicationUser = await _dbContext.Users
                .OfType<ApplicationUser>()
                .FirstOrDefaultAsync(u => u.DiscordUserId == discordUserId, cancellationToken);

            if (applicationUser != null)
            {
                counts["UserGuildAccess"] = await _dbContext.UserGuildAccess.CountAsync(uga => uga.ApplicationUserId == applicationUser.Id, cancellationToken);
                counts["UserDiscordGuilds"] = await _dbContext.UserDiscordGuilds.CountAsync(udg => udg.ApplicationUserId == applicationUser.Id, cancellationToken);
                counts["DiscordOAuthTokens"] = await _dbContext.DiscordOAuthTokens.CountAsync(t => t.ApplicationUserId == applicationUser.Id, cancellationToken);
                counts["ApplicationUser"] = 1;
            }

            _logger.LogDebug(
                "Purge preview for Discord user {DiscordUserId}: {Counts}",
                discordUserId, System.Text.Json.JsonSerializer.Serialize(counts));

            activity?.SetTag("preview.total_count", counts.Values.Sum());
            BotActivitySource.SetSuccess(activity);

            return UserPurgeResultDto.Succeeded(counts, string.Empty);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error previewing purge for Discord user {DiscordUserId}",
                discordUserId);

            BotActivitySource.RecordException(activity, ex);

            return UserPurgeResultDto.Failed(
                UserPurgeResultDto.DatabaseError,
                "An error occurred while previewing purge data");
        }
    }

    public async Task<(bool CanPurge, string? BlockingReason)> CanPurgeUserAsync(
        ulong discordUserId,
        CancellationToken cancellationToken = default)
    {
        using var activity = BotActivitySource.StartServiceActivity(
            "userpurge",
            "can_purge_user",
            userId: discordUserId);

        try
        {
            _logger.LogDebug("Checking if Discord user {DiscordUserId} can be purged", discordUserId);

            // Check if user has a linked ApplicationUser account
            var applicationUser = await _dbContext.Users
                .OfType<ApplicationUser>()
                .FirstOrDefaultAsync(u => u.DiscordUserId == discordUserId, cancellationToken);

            if (applicationUser == null)
            {
                // No linked account, can purge
                _logger.LogDebug("Discord user {DiscordUserId} has no linked account, can purge", discordUserId);
                activity?.SetTag("can_purge", true);
                BotActivitySource.SetSuccess(activity);
                return (true, null);
            }

            // Check if user has admin roles
            var roles = await _userManager.GetRolesAsync(applicationUser);
            var adminRoles = roles.Where(r =>
                r == Roles.SuperAdmin ||
                r == Roles.Admin).ToList();

            if (adminRoles.Any())
            {
                var blockingReason = $"User has administrative role(s): {string.Join(", ", adminRoles)}. Remove admin privileges before purging.";

                _logger.LogDebug(
                    "Discord user {DiscordUserId} cannot be purged: {BlockingReason}",
                    discordUserId, blockingReason);

                activity?.SetTag("can_purge", false);
                activity?.SetTag("blocking_reason", "admin_roles");
                BotActivitySource.SetSuccess(activity);

                return (false, blockingReason);
            }

            // No blocking conditions found
            _logger.LogDebug("Discord user {DiscordUserId} can be purged", discordUserId);
            activity?.SetTag("can_purge", true);
            BotActivitySource.SetSuccess(activity);

            return (true, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error checking if Discord user {DiscordUserId} can be purged",
                discordUserId);

            BotActivitySource.RecordException(activity, ex);
            throw;
        }
    }

    /// <summary>
    /// Invalidates all cached data for the specified user.
    /// </summary>
    private void InvalidateUserCaches(ulong discordUserId)
    {
        try
        {
            // Invalidate consent cache
            foreach (ConsentType consentType in Enum.GetValues(typeof(ConsentType)))
            {
                var cacheKey = $"consent:{discordUserId}:{consentType}";
                _cache.Remove(cacheKey);
            }

            // Invalidate other user-related caches as needed
            // (Add more cache keys if other services cache user data)

            _logger.LogDebug("Invalidated caches for Discord user {DiscordUserId}", discordUserId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error invalidating caches for Discord user {DiscordUserId}", discordUserId);
        }
    }
}
