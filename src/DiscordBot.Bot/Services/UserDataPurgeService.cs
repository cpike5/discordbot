using DiscordBot.Core.DTOs;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using DiscordBot.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DiscordBot.Bot.Services;

/// <summary>
/// Service for purging user data from the system.
/// Provides methods for privacy compliance and "right to be forgotten" requests.
/// </summary>
public class UserDataPurgeService : IUserDataPurgeService
{
    private readonly BotDbContext _dbContext;
    private readonly IAuditLogService _auditLogService;
    private readonly ILogger<UserDataPurgeService> _logger;

    public UserDataPurgeService(
        BotDbContext dbContext,
        IAuditLogService auditLogService,
        ILogger<UserDataPurgeService> logger)
    {
        _dbContext = dbContext;
        _auditLogService = auditLogService;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<UserDataSummary> GetUserDataSummaryAsync(
        ulong discordUserId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Fetching user data summary for Discord user ID: {DiscordUserId}", discordUserId);

        try
        {
            var userExists = await _dbContext.Users
                .AnyAsync(u => u.Id == discordUserId, cancellationToken);

            var counts = await GetUserDataCountsAsync(discordUserId, cancellationToken);

            _logger.LogInformation(
                "User data summary for {DiscordUserId}: UserExists={UserExists}, TotalRecords={TotalRecords}",
                discordUserId, userExists, counts.TotalRecords);

            return new UserDataSummary
            {
                DiscordUserId = discordUserId,
                UserExists = userExists,
                Counts = counts
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching user data summary for Discord user ID: {DiscordUserId}", discordUserId);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<UserDataPurgeResult> PurgeUserDataAsync(
        ulong discordUserId,
        string initiatedBy,
        string? reason = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogWarning(
            "Starting user data purge operation for Discord user ID: {DiscordUserId}, initiated by: {InitiatedBy}, reason: {Reason}",
            discordUserId, initiatedBy, reason ?? "Not specified");

        try
        {
            // Check if user exists
            var userExists = await _dbContext.Users
                .AnyAsync(u => u.Id == discordUserId, cancellationToken);

            if (!userExists)
            {
                _logger.LogWarning("Cannot purge data: User {DiscordUserId} not found", discordUserId);
                return UserDataPurgeResult.Failure(
                    UserDataPurgeResult.UserNotFound,
                    $"User with Discord ID {discordUserId} not found");
            }

            // Start transaction for atomicity
            await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

            try
            {
                var counts = new UserDataPurgeCounts();

                // Delete and anonymize records in order (foreign key dependencies considered)
                // Order matters: delete child records before parent records

                // 1. Delete MemberActivitySnapshots
                var memberActivitySnapshotsDeleted = await _dbContext.MemberActivitySnapshots
                    .Where(m => m.UserId == discordUserId)
                    .ExecuteDeleteAsync(cancellationToken);
                counts = counts with { MemberActivitySnapshots = memberActivitySnapshotsDeleted };
                _logger.LogDebug("Deleted {Count} MemberActivitySnapshot records", memberActivitySnapshotsDeleted);

                // 2. Delete/Anonymize Watchlists
                // Delete where user is the target, anonymize where user is the moderator
                var watchlistsDeleted = await _dbContext.Watchlists
                    .Where(w => w.UserId == discordUserId)
                    .ExecuteDeleteAsync(cancellationToken);

                var watchlistsAnonymized = await _dbContext.Watchlists
                    .Where(w => w.AddedByUserId == discordUserId)
                    .ExecuteUpdateAsync(
                        setters => setters.SetProperty(w => w.AddedByUserId, (ulong)0),
                        cancellationToken);

                counts = counts with { Watchlists = watchlistsDeleted };
                _logger.LogDebug("Deleted {DeletedCount} Watchlist records (target), anonymized {AnonymizedCount} (moderator)",
                    watchlistsDeleted, watchlistsAnonymized);

                // 3. Delete/Anonymize UserModTags
                var userModTagsDeleted = await _dbContext.UserModTags
                    .Where(t => t.UserId == discordUserId)
                    .ExecuteDeleteAsync(cancellationToken);

                var userModTagsAnonymized = await _dbContext.UserModTags
                    .Where(t => t.AppliedByUserId == discordUserId)
                    .ExecuteUpdateAsync(
                        setters => setters.SetProperty(t => t.AppliedByUserId, (ulong)0),
                        cancellationToken);

                counts = counts with { UserModTags = userModTagsDeleted };
                _logger.LogDebug("Deleted {DeletedCount} UserModTag records (target), anonymized {AnonymizedCount} (moderator)",
                    userModTagsDeleted, userModTagsAnonymized);

                // 4. Delete/Anonymize ModNotes
                var modNotesDeleted = await _dbContext.ModNotes
                    .Where(n => n.TargetUserId == discordUserId)
                    .ExecuteDeleteAsync(cancellationToken);

                var modNotesAnonymized = await _dbContext.ModNotes
                    .Where(n => n.AuthorUserId == discordUserId)
                    .ExecuteUpdateAsync(
                        setters => setters.SetProperty(n => n.AuthorUserId, (ulong)0),
                        cancellationToken);

                counts = counts with { ModNotes = modNotesDeleted };
                _logger.LogDebug("Deleted {DeletedCount} ModNote records (target), anonymized {AnonymizedCount} (author)",
                    modNotesDeleted, modNotesAnonymized);

                // 5. Delete/Anonymize ModerationCases
                var moderationCasesDeleted = await _dbContext.ModerationCases
                    .Where(c => c.TargetUserId == discordUserId)
                    .ExecuteDeleteAsync(cancellationToken);

                var moderationCasesAnonymized = await _dbContext.ModerationCases
                    .Where(c => c.ModeratorUserId == discordUserId)
                    .ExecuteUpdateAsync(
                        setters => setters.SetProperty(c => c.ModeratorUserId, (ulong)0),
                        cancellationToken);

                counts = counts with { ModerationCases = moderationCasesDeleted };
                _logger.LogDebug("Deleted {DeletedCount} ModerationCase records (target), anonymized {AnonymizedCount} (moderator)",
                    moderationCasesDeleted, moderationCasesAnonymized);

                // 6. Delete/Anonymize FlaggedEvents
                var flaggedEventsDeleted = await _dbContext.FlaggedEvents
                    .Where(e => e.UserId == discordUserId)
                    .ExecuteDeleteAsync(cancellationToken);

                var flaggedEventsAnonymized = await _dbContext.FlaggedEvents
                    .Where(e => e.ReviewedByUserId == discordUserId)
                    .ExecuteUpdateAsync(
                        setters => setters.SetProperty(e => e.ReviewedByUserId, (ulong?)null),
                        cancellationToken);

                counts = counts with { FlaggedEvents = flaggedEventsDeleted };
                _logger.LogDebug("Deleted {DeletedCount} FlaggedEvent records (target), anonymized {AnonymizedCount} (reviewer)",
                    flaggedEventsDeleted, flaggedEventsAnonymized);

                // 7. Delete Reminders
                var remindersDeleted = await _dbContext.Reminders
                    .Where(r => r.UserId == discordUserId)
                    .ExecuteDeleteAsync(cancellationToken);
                counts = counts with { Reminders = remindersDeleted };
                _logger.LogDebug("Deleted {Count} Reminder records", remindersDeleted);

                // 8. Delete RatRecords
                var ratRecordsDeleted = await _dbContext.RatRecords
                    .Where(r => r.UserId == discordUserId)
                    .ExecuteDeleteAsync(cancellationToken);
                counts = counts with { RatRecords = ratRecordsDeleted };
                _logger.LogDebug("Deleted {Count} RatRecord records", ratRecordsDeleted);

                // 9. Delete RatVotes
                var ratVotesDeleted = await _dbContext.RatVotes
                    .Where(v => v.VoterUserId == discordUserId)
                    .ExecuteDeleteAsync(cancellationToken);
                counts = counts with { RatVotes = ratVotesDeleted };
                _logger.LogDebug("Deleted {Count} RatVote records", ratVotesDeleted);

                // 10. Delete RatWatches (where user is either accused or initiator)
                var ratWatchesDeleted = await _dbContext.RatWatches
                    .Where(w => w.AccusedUserId == discordUserId || w.InitiatorUserId == discordUserId)
                    .ExecuteDeleteAsync(cancellationToken);
                counts = counts with { RatWatches = ratWatchesDeleted };
                _logger.LogDebug("Deleted {Count} RatWatch records", ratWatchesDeleted);

                // 11. Delete UserConsents
                var userConsentsDeleted = await _dbContext.UserConsents
                    .Where(c => c.DiscordUserId == discordUserId)
                    .ExecuteDeleteAsync(cancellationToken);
                counts = counts with { UserConsents = userConsentsDeleted };
                _logger.LogDebug("Deleted {Count} UserConsent records", userConsentsDeleted);

                // 12. Delete GuildMembers
                var guildMembersDeleted = await _dbContext.GuildMembers
                    .Where(m => m.UserId == discordUserId)
                    .ExecuteDeleteAsync(cancellationToken);
                counts = counts with { GuildMembers = guildMembersDeleted };
                _logger.LogDebug("Deleted {Count} GuildMember records", guildMembersDeleted);

                // 13. Delete MessageLogs
                var messageLogsDeleted = await _dbContext.MessageLogs
                    .Where(l => l.AuthorId == discordUserId)
                    .ExecuteDeleteAsync(cancellationToken);
                counts = counts with { MessageLogs = messageLogsDeleted };
                _logger.LogDebug("Deleted {Count} MessageLog records", messageLogsDeleted);

                // 14. Delete CommandLogs
                var commandLogsDeleted = await _dbContext.CommandLogs
                    .Where(l => l.UserId == discordUserId)
                    .ExecuteDeleteAsync(cancellationToken);
                counts = counts with { CommandLogs = commandLogsDeleted };
                _logger.LogDebug("Deleted {Count} CommandLog records", commandLogsDeleted);

                // 15. Finally, delete the User entity itself
                var usersDeleted = await _dbContext.Users
                    .Where(u => u.Id == discordUserId)
                    .ExecuteDeleteAsync(cancellationToken);
                counts = counts with { Users = usersDeleted };
                _logger.LogDebug("Deleted {Count} User records", usersDeleted);

                // Commit the transaction
                await transaction.CommitAsync(cancellationToken);

                _logger.LogWarning(
                    "Successfully purged user data for Discord user ID: {DiscordUserId}. Total records deleted: {TotalRecords}",
                    discordUserId, counts.TotalRecords);

                // Log audit entry after successful purge
                try
                {
                    _auditLogService.CreateBuilder()
                        .ForCategory(AuditLogCategory.User)
                        .WithAction(AuditLogAction.DataPurged)
                        .ByUser(initiatedBy)
                        .OnTarget("User", discordUserId.ToString())
                        .WithDetails(new
                        {
                            discordUserId = discordUserId.ToString(),
                            reason,
                            deletedCounts = new
                            {
                                commandLogs = counts.CommandLogs,
                                messageLogs = counts.MessageLogs,
                                guildMembers = counts.GuildMembers,
                                userConsents = counts.UserConsents,
                                ratWatches = counts.RatWatches,
                                ratVotes = counts.RatVotes,
                                ratRecords = counts.RatRecords,
                                reminders = counts.Reminders,
                                flaggedEvents = counts.FlaggedEvents,
                                moderationCases = counts.ModerationCases,
                                modNotes = counts.ModNotes,
                                userModTags = counts.UserModTags,
                                watchlists = counts.Watchlists,
                                memberActivitySnapshots = counts.MemberActivitySnapshots,
                                users = counts.Users,
                                totalRecords = counts.TotalRecords
                            },
                            initiatedBy
                        })
                        .Enqueue();
                }
                catch (Exception auditEx)
                {
                    _logger.LogError(auditEx,
                        "Failed to log audit entry for user data purge {DiscordUserId}",
                        discordUserId);
                }

                return UserDataPurgeResult.Success(discordUserId, counts);
            }
            catch (OperationCanceledException)
            {
                await transaction.RollbackAsync(CancellationToken.None);
                _logger.LogWarning("User data purge operation cancelled for Discord user ID: {DiscordUserId}", discordUserId);
                throw;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(CancellationToken.None);
                _logger.LogError(ex,
                    "Database error during user data purge for Discord user ID: {DiscordUserId}",
                    discordUserId);
                throw;
            }
        }
        catch (OperationCanceledException)
        {
            return UserDataPurgeResult.Failure(
                UserDataPurgeResult.OperationCancelled,
                "The purge operation was cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to purge user data for Discord user ID: {DiscordUserId}", discordUserId);
            return UserDataPurgeResult.Failure(
                UserDataPurgeResult.DatabaseError,
                $"An error occurred while purging user data: {ex.Message}");
        }
    }

    #region Private Helper Methods

    /// <summary>
    /// Gets counts of all user data entities for the specified Discord user ID.
    /// </summary>
    private async Task<UserDataPurgeCounts> GetUserDataCountsAsync(
        ulong discordUserId,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("Counting user data records for Discord user ID: {DiscordUserId}", discordUserId);

        // Execute all count queries in parallel for performance
        var commandLogsTask = _dbContext.CommandLogs
            .Where(l => l.UserId == discordUserId)
            .CountAsync(cancellationToken);

        var messageLogsTask = _dbContext.MessageLogs
            .Where(l => l.AuthorId == discordUserId)
            .CountAsync(cancellationToken);

        var guildMembersTask = _dbContext.GuildMembers
            .Where(m => m.UserId == discordUserId)
            .CountAsync(cancellationToken);

        var userConsentsTask = _dbContext.UserConsents
            .Where(c => c.DiscordUserId == discordUserId)
            .CountAsync(cancellationToken);

        var ratWatchesTask = _dbContext.RatWatches
            .Where(w => w.AccusedUserId == discordUserId || w.InitiatorUserId == discordUserId)
            .CountAsync(cancellationToken);

        var ratVotesTask = _dbContext.RatVotes
            .Where(v => v.VoterUserId == discordUserId)
            .CountAsync(cancellationToken);

        var ratRecordsTask = _dbContext.RatRecords
            .Where(r => r.UserId == discordUserId)
            .CountAsync(cancellationToken);

        var remindersTask = _dbContext.Reminders
            .Where(r => r.UserId == discordUserId)
            .CountAsync(cancellationToken);

        var flaggedEventsTask = _dbContext.FlaggedEvents
            .Where(e => e.UserId == discordUserId || e.ReviewedByUserId == discordUserId)
            .CountAsync(cancellationToken);

        var moderationCasesTask = _dbContext.ModerationCases
            .Where(c => c.TargetUserId == discordUserId || c.ModeratorUserId == discordUserId)
            .CountAsync(cancellationToken);

        var modNotesTask = _dbContext.ModNotes
            .Where(n => n.TargetUserId == discordUserId || n.AuthorUserId == discordUserId)
            .CountAsync(cancellationToken);

        var userModTagsTask = _dbContext.UserModTags
            .Where(t => t.UserId == discordUserId || t.AppliedByUserId == discordUserId)
            .CountAsync(cancellationToken);

        var watchlistsTask = _dbContext.Watchlists
            .Where(w => w.UserId == discordUserId || w.AddedByUserId == discordUserId)
            .CountAsync(cancellationToken);

        var memberActivitySnapshotsTask = _dbContext.MemberActivitySnapshots
            .Where(m => m.UserId == discordUserId)
            .CountAsync(cancellationToken);

        var usersTask = _dbContext.Users
            .Where(u => u.Id == discordUserId)
            .CountAsync(cancellationToken);

        // Await all tasks
        await Task.WhenAll(
            commandLogsTask,
            messageLogsTask,
            guildMembersTask,
            userConsentsTask,
            ratWatchesTask,
            ratVotesTask,
            ratRecordsTask,
            remindersTask,
            flaggedEventsTask,
            moderationCasesTask,
            modNotesTask,
            userModTagsTask,
            watchlistsTask,
            memberActivitySnapshotsTask,
            usersTask);

        return new UserDataPurgeCounts
        {
            CommandLogs = await commandLogsTask,
            MessageLogs = await messageLogsTask,
            GuildMembers = await guildMembersTask,
            UserConsents = await userConsentsTask,
            RatWatches = await ratWatchesTask,
            RatVotes = await ratVotesTask,
            RatRecords = await ratRecordsTask,
            Reminders = await remindersTask,
            FlaggedEvents = await flaggedEventsTask,
            ModerationCases = await moderationCasesTask,
            ModNotes = await modNotesTask,
            UserModTags = await userModTagsTask,
            Watchlists = await watchlistsTask,
            MemberActivitySnapshots = await memberActivitySnapshotsTask,
            Users = await usersTask
        };
    }

    #endregion
}
