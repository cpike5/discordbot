using DiscordBot.Core.DTOs;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DiscordBot.Infrastructure.Data.Repositories;

/// <summary>
/// Repository implementation for RatRecord entities with record-specific operations.
/// </summary>
public class RatRecordRepository : Repository<RatRecord>, IRatRecordRepository
{
    private readonly ILogger<RatRecordRepository> _logger;

    public RatRecordRepository(
        BotDbContext context,
        ILogger<RatRecordRepository> logger,
        ILogger<Repository<RatRecord>> baseLogger)
        : base(context, baseLogger)
    {
        _logger = logger;
    }

    public async Task<int> GetGuiltyCountAsync(
        ulong guildId,
        ulong userId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Counting guilty records for user {UserId} in guild {GuildId}", userId, guildId);

        var count = await DbSet
            .AsNoTracking()
            .Where(r => r.GuildId == guildId && r.UserId == userId)
            .CountAsync(cancellationToken);

        _logger.LogDebug("User {UserId} has {Count} guilty records", userId, count);
        return count;
    }

    public async Task<IEnumerable<RatRecord>> GetRecentRecordsAsync(
        ulong guildId,
        ulong userId,
        int limit = 5,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug(
            "Retrieving {Limit} recent guilty records for user {UserId} in guild {GuildId}",
            limit, userId, guildId);

        var records = await DbSet
            .AsNoTracking()
            .Where(r => r.GuildId == guildId && r.UserId == userId)
            .OrderByDescending(r => r.RecordedAt)
            .Take(limit)
            .ToListAsync(cancellationToken);

        _logger.LogDebug("Retrieved {Count} recent records for user {UserId}", records.Count, userId);
        return records;
    }

    public async Task<IEnumerable<(ulong UserId, int GuiltyCount)>> GetLeaderboardAsync(
        ulong guildId,
        int limit = 10,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Retrieving top {Limit} leaderboard for guild {GuildId}", limit, guildId);

        var leaderboard = await DbSet
            .AsNoTracking()
            .Where(r => r.GuildId == guildId)
            .GroupBy(r => r.UserId)
            .Select(g => new
            {
                UserId = g.Key,
                GuiltyCount = g.Count()
            })
            .OrderByDescending(x => x.GuiltyCount)
            .Take(limit)
            .ToListAsync(cancellationToken);

        var result = leaderboard.Select(x => (x.UserId, x.GuiltyCount));

        _logger.LogDebug("Retrieved {Count} leaderboard entries for guild {GuildId}", result.Count(), guildId);
        return result;
    }

    public async Task<IEnumerable<RatWatchUserMetricsDto>> GetUserMetricsAsync(
        ulong guildId,
        string sortBy,
        int limit,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug(
            "Retrieving user metrics for guild {GuildId}, sorted by {SortBy}, limit {Limit}",
            guildId, sortBy, limit);

        // Get all watches for the guild with their statuses
        var watches = Context.Set<RatWatch>()
            .AsNoTracking()
            .Where(w => w.GuildId == guildId);

        // Group by accused user and calculate metrics
        var metricsQuery = watches
            .GroupBy(w => w.AccusedUserId)
            .Select(g => new
            {
                UserId = g.Key,
                WatchesAgainst = g.Count(),
                GuiltyCount = g.Count(w => w.Status == RatWatchStatus.Guilty),
                EarlyCheckInCount = g.Count(w => w.Status == RatWatchStatus.ClearedEarly),
                LastIncidentDate = g.OrderByDescending(w => w.CreatedAt).Select(w => w.CreatedAt).FirstOrDefault()
            });

        // Calculate accountability score and project to DTO
        var metrics = await metricsQuery
            .ToListAsync(cancellationToken);

        var userMetrics = metrics.Select(m => new RatWatchUserMetricsDto
        {
            UserId = m.UserId,
            Username = string.Empty, // Populated by service layer
            WatchesAgainst = m.WatchesAgainst,
            GuiltyCount = m.GuiltyCount,
            EarlyCheckInCount = m.EarlyCheckInCount,
            AccountabilityScore = m.WatchesAgainst > 0 ? (double)m.EarlyCheckInCount / m.WatchesAgainst * 100 : 0,
            LastIncidentDate = m.LastIncidentDate
        });

        // Apply sorting
        var sortedMetrics = sortBy.ToLowerInvariant() switch
        {
            "watched" => userMetrics.OrderByDescending(m => m.WatchesAgainst),
            "guilty" => userMetrics.OrderByDescending(m => m.GuiltyCount),
            "accountability" => userMetrics.OrderByDescending(m => m.AccountabilityScore),
            _ => userMetrics.OrderByDescending(m => m.WatchesAgainst)
        };

        var result = sortedMetrics.Take(limit).ToList();

        _logger.LogDebug("Retrieved {Count} user metrics for guild {GuildId}", result.Count, guildId);
        return result;
    }

    public async Task<RatWatchFunStatsDto> GetFunStatsAsync(
        ulong guildId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Retrieving fun stats for guild {GuildId}", guildId);

        var watches = await Context.Set<RatWatch>()
            .AsNoTracking()
            .Where(w => w.GuildId == guildId)
            .Include(w => w.Votes)
            .OrderBy(w => w.CreatedAt)
            .ToListAsync(cancellationToken);

        // Calculate streaks (in-memory due to complexity)
        var guiltyStreak = CalculateLongestStreak(
            watches,
            w => w.Status == RatWatchStatus.Guilty);

        var cleanStreak = CalculateLongestStreak(
            watches,
            w => w.Status == RatWatchStatus.ClearedEarly);

        // Find biggest landslide (largest vote margin)
        var votingWatches = watches
            .Where(w => w.Status == RatWatchStatus.Guilty || w.Status == RatWatchStatus.NotGuilty)
            .ToList();

        var biggestLandslide = votingWatches
            .Select(w => new
            {
                Watch = w,
                GuiltyVotes = w.Votes.Count(v => v.IsGuiltyVote),
                NotGuiltyVotes = w.Votes.Count(v => !v.IsGuiltyVote)
            })
            .OrderByDescending(x => Math.Abs(x.GuiltyVotes - x.NotGuiltyVotes))
            .FirstOrDefault();

        // Find closest call (smallest margin that still resulted in guilty)
        var guiltyVotes = votingWatches
            .Where(w => w.Status == RatWatchStatus.Guilty)
            .Select(w => new
            {
                Watch = w,
                GuiltyVotes = w.Votes.Count(v => v.IsGuiltyVote),
                NotGuiltyVotes = w.Votes.Count(v => !v.IsGuiltyVote)
            })
            .OrderBy(x => Math.Abs(x.GuiltyVotes - x.NotGuiltyVotes))
            .FirstOrDefault();

        // Find fastest check-in
        var clearedEarlyWatches = watches
            .Where(w => w.Status == RatWatchStatus.ClearedEarly && w.ClearedAt.HasValue)
            .Select(w => new
            {
                Watch = w,
                Duration = w.ClearedAt!.Value - w.CreatedAt
            })
            .OrderBy(x => x.Duration)
            .FirstOrDefault();

        // Find latest check-in (closest to deadline but still early)
        var latestCheckIn = watches
            .Where(w => w.Status == RatWatchStatus.ClearedEarly && w.ClearedAt.HasValue)
            .Select(w => new
            {
                Watch = w,
                TimeBeforeDeadline = w.ScheduledAt - w.ClearedAt!.Value
            })
            .OrderBy(x => x.TimeBeforeDeadline)
            .FirstOrDefault();

        _logger.LogDebug("Calculated fun stats for guild {GuildId}", guildId);

        return new RatWatchFunStatsDto
        {
            LongestGuiltyStreak = guiltyStreak != null ? new UserStreakDto
            {
                UserId = guiltyStreak.Value.UserId,
                Username = string.Empty, // Populated by service layer
                StreakCount = guiltyStreak.Value.Count
            } : null,
            LongestCleanStreak = cleanStreak != null ? new UserStreakDto
            {
                UserId = cleanStreak.Value.UserId,
                Username = string.Empty, // Populated by service layer
                StreakCount = cleanStreak.Value.Count
            } : null,
            BiggestLandslide = biggestLandslide != null ? new IncidentHighlightDto
            {
                WatchId = biggestLandslide.Watch.Id,
                UserId = biggestLandslide.Watch.AccusedUserId,
                Username = string.Empty, // Populated by service layer
                Description = $"{biggestLandslide.GuiltyVotes}-{biggestLandslide.NotGuiltyVotes} {(biggestLandslide.Watch.Status == RatWatchStatus.Guilty ? "Guilty" : "Not Guilty")}",
                Date = biggestLandslide.Watch.CreatedAt
            } : null,
            ClosestCall = guiltyVotes != null ? new IncidentHighlightDto
            {
                WatchId = guiltyVotes.Watch.Id,
                UserId = guiltyVotes.Watch.AccusedUserId,
                Username = string.Empty, // Populated by service layer
                Description = $"{guiltyVotes.GuiltyVotes}-{guiltyVotes.NotGuiltyVotes} Guilty",
                Date = guiltyVotes.Watch.CreatedAt
            } : null,
            FastestCheckIn = clearedEarlyWatches != null ? new IncidentHighlightDto
            {
                WatchId = clearedEarlyWatches.Watch.Id,
                UserId = clearedEarlyWatches.Watch.AccusedUserId,
                Username = string.Empty, // Populated by service layer
                Description = $"Cleared in {FormatDuration(clearedEarlyWatches.Duration)}",
                Date = clearedEarlyWatches.Watch.CreatedAt
            } : null,
            LatestCheckIn = latestCheckIn != null ? new IncidentHighlightDto
            {
                WatchId = latestCheckIn.Watch.Id,
                UserId = latestCheckIn.Watch.AccusedUserId,
                Username = string.Empty, // Populated by service layer
                Description = $"Cleared {FormatDuration(latestCheckIn.TimeBeforeDeadline)} before deadline",
                Date = latestCheckIn.Watch.CreatedAt
            } : null
        };
    }

    /// <summary>
    /// Calculates the longest streak for a given condition grouped by user.
    /// Returns the user with the longest streak and the count.
    /// </summary>
    private (ulong UserId, int Count)? CalculateLongestStreak(
        List<RatWatch> watches,
        Func<RatWatch, bool> condition)
    {
        var userStreaks = new Dictionary<ulong, int>();
        var currentStreaks = new Dictionary<ulong, int>();

        // Group by user and process in chronological order
        var watchesByUser = watches
            .GroupBy(w => w.AccusedUserId)
            .ToDictionary(g => g.Key, g => g.OrderBy(w => w.CreatedAt).ToList());

        foreach (var (userId, userWatches) in watchesByUser)
        {
            var currentStreak = 0;
            var maxStreak = 0;

            foreach (var watch in userWatches)
            {
                if (condition(watch))
                {
                    currentStreak++;
                    maxStreak = Math.Max(maxStreak, currentStreak);
                }
                else
                {
                    currentStreak = 0;
                }
            }

            if (maxStreak > 0)
            {
                userStreaks[userId] = maxStreak;
            }
        }

        if (userStreaks.Count == 0)
            return null;

        var longestStreak = userStreaks.OrderByDescending(x => x.Value).First();
        return (longestStreak.Key, longestStreak.Value);
    }

    /// <summary>
    /// Formats a TimeSpan into a human-readable string.
    /// </summary>
    private string FormatDuration(TimeSpan duration)
    {
        if (duration.TotalSeconds < 60)
            return $"{(int)duration.TotalSeconds} seconds";

        if (duration.TotalMinutes < 60)
            return $"{(int)duration.TotalMinutes} minutes";

        if (duration.TotalHours < 24)
            return $"{duration.Hours} hours {duration.Minutes} minutes";

        return $"{(int)duration.TotalDays} days {duration.Hours} hours";
    }
}
