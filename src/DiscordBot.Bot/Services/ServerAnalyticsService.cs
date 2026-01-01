using DiscordBot.Core.DTOs;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DiscordBot.Bot.Services;

/// <summary>
/// Service for generating server activity analytics and metrics.
/// Aggregates data from member activity snapshots, channel activity, and guild metrics.
/// </summary>
public class ServerAnalyticsService : IServerAnalyticsService
{
    private readonly IMemberActivityRepository _memberActivityRepository;
    private readonly IChannelActivityRepository _channelActivityRepository;
    private readonly IGuildMetricsRepository _guildMetricsRepository;
    private readonly ILogger<ServerAnalyticsService> _logger;

    public ServerAnalyticsService(
        IMemberActivityRepository memberActivityRepository,
        IChannelActivityRepository channelActivityRepository,
        IGuildMetricsRepository guildMetricsRepository,
        ILogger<ServerAnalyticsService> logger)
    {
        _memberActivityRepository = memberActivityRepository;
        _channelActivityRepository = channelActivityRepository;
        _guildMetricsRepository = guildMetricsRepository;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<ServerAnalyticsSummaryDto> GetSummaryAsync(
        ulong guildId,
        DateTime start,
        DateTime end,
        CancellationToken ct = default)
    {
        _logger.LogDebug(
            "Generating server analytics summary for guild {GuildId} from {Start} to {End}",
            guildId, start, end);

        // Get the most recent guild metrics snapshot for current member counts
        var latestMetrics = await _guildMetricsRepository.GetLatestAsync(guildId, ct);

        // Calculate time ranges for different periods
        var now = DateTime.UtcNow;
        var last24h = now.AddHours(-24);
        var last7d = now.AddDays(-7);
        var last30d = now.AddDays(-30);

        // Get activity data for the requested time period using daily granularity
        var activityData = await _memberActivityRepository.GetActivityTimeSeriesAsync(
            guildId,
            start,
            end,
            SnapshotGranularity.Daily,
            ct);

        // Calculate active members for different time windows
        var dailySnapshots = await _memberActivityRepository.GetActivityTimeSeriesAsync(
            guildId,
            last30d,
            now,
            SnapshotGranularity.Daily,
            ct);

        // Get unique active members for each period
        var activeMembersLast24h = dailySnapshots
            .Where(x => x.Period >= last24h)
            .Sum(x => x.ActiveMembers);

        var activeMembersLast7d = dailySnapshots
            .Where(x => x.Period >= last7d)
            .Sum(x => x.ActiveMembers);

        var activeMembersLast30d = dailySnapshots
            .Sum(x => x.ActiveMembers);

        // Calculate message counts
        var messagesLast24h = dailySnapshots
            .Where(x => x.Period >= last24h)
            .Sum(x => x.TotalMessages);

        var messagesLast7d = dailySnapshots
            .Where(x => x.Period >= last7d)
            .Sum(x => x.TotalMessages);

        // Calculate member growth from guild metrics
        var metricsLast7d = await _guildMetricsRepository.GetByDateRangeAsync(
            guildId,
            DateOnly.FromDateTime(last7d),
            DateOnly.FromDateTime(now),
            ct);

        var oldestMetric = metricsLast7d.FirstOrDefault();
        var memberGrowth7d = latestMetrics != null && oldestMetric != null
            ? latestMetrics.TotalMembers - oldestMetric.TotalMembers
            : 0;

        var memberGrowthPercent = oldestMetric?.TotalMembers > 0
            ? ((decimal)memberGrowth7d / oldestMetric.TotalMembers) * 100
            : 0m;

        // Get count of active channels (channels with messages in the time period)
        var channelRankings = await _channelActivityRepository.GetChannelRankingsAsync(
            guildId,
            start,
            end,
            limit: 1000, // Get all channels to count them
            ct);

        _logger.LogInformation(
            "Server analytics summary generated for guild {GuildId}: {TotalMembers} members, {ActiveMembers24h} active (24h)",
            guildId, latestMetrics?.TotalMembers ?? 0, activeMembersLast24h);

        return new ServerAnalyticsSummaryDto
        {
            TotalMembers = latestMetrics?.TotalMembers ?? 0,
            OnlineMembers = 0, // Online member count not tracked in snapshots
            ActiveMembers24h = activeMembersLast24h,
            ActiveMembers7d = activeMembersLast7d,
            ActiveMembers30d = activeMembersLast30d,
            Messages24h = messagesLast24h,
            Messages7d = messagesLast7d,
            MemberGrowth7d = memberGrowth7d,
            MemberGrowthPercent = memberGrowthPercent,
            ActiveChannels = channelRankings.Count
        };
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<ActivityTimeSeriesDto>> GetActivityTimeSeriesAsync(
        ulong guildId,
        DateTime start,
        DateTime end,
        CancellationToken ct = default)
    {
        _logger.LogDebug(
            "Generating activity time series for guild {GuildId} from {Start} to {End}",
            guildId, start, end);

        // Get daily activity aggregates
        var activityData = await _memberActivityRepository.GetActivityTimeSeriesAsync(
            guildId,
            start,
            end,
            SnapshotGranularity.Daily,
            ct);

        // Get channel activity for the same period
        var channelRankings = await _channelActivityRepository.GetChannelRankingsAsync(
            guildId,
            start,
            end,
            limit: 1000,
            ct);

        // Create a dictionary of channel counts by date (we'll need to aggregate this differently)
        // For now, we'll use the total unique channels as a constant
        var activeChannelsCount = channelRankings.Count;

        var results = activityData.Select(x => new ActivityTimeSeriesDto
        {
            Date = x.Period,
            MessageCount = x.TotalMessages,
            ActiveMembers = x.ActiveMembers,
            ActiveChannels = activeChannelsCount // This is simplified - could be improved with per-day channel tracking
        }).ToList();

        _logger.LogDebug(
            "Generated {Count} activity time series data points for guild {GuildId}",
            results.Count, guildId);

        return results;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<ServerActivityHeatmapDto>> GetActivityHeatmapAsync(
        ulong guildId,
        DateTime start,
        DateTime end,
        CancellationToken ct = default)
    {
        _logger.LogDebug(
            "Generating activity heatmap for guild {GuildId} from {Start} to {End}",
            guildId, start, end);

        // Get hourly snapshots for the heatmap
        var hourlyData = await _memberActivityRepository.GetActivityTimeSeriesAsync(
            guildId,
            start,
            end,
            SnapshotGranularity.Hourly,
            ct);

        // Group by day of week and hour
        var heatmapData = hourlyData
            .GroupBy(x => new
            {
                DayOfWeek = (int)x.Period.DayOfWeek,
                Hour = x.Period.Hour
            })
            .Select(g => new ServerActivityHeatmapDto
            {
                DayOfWeek = g.Key.DayOfWeek,
                Hour = g.Key.Hour,
                MessageCount = g.Sum(x => (long)x.TotalMessages)
            })
            .OrderBy(x => x.DayOfWeek)
            .ThenBy(x => x.Hour)
            .ToList();

        _logger.LogDebug(
            "Generated heatmap with {Count} data points for guild {GuildId}",
            heatmapData.Count, guildId);

        return heatmapData;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<TopContributorDto>> GetTopContributorsAsync(
        ulong guildId,
        DateTime start,
        DateTime end,
        int limit = 10,
        CancellationToken ct = default)
    {
        _logger.LogDebug(
            "Getting top {Limit} contributors for guild {GuildId} from {Start} to {End}",
            limit, guildId, start, end);

        // Get top active members from the repository
        var topMembers = await _memberActivityRepository.GetTopActiveMembersAsync(
            guildId,
            start,
            end,
            limit,
            ct);

        // Transform to TopContributorDto
        // Note: The repository returns aggregated snapshots, we need to sum up the message counts
        var contributors = topMembers
            .GroupBy(x => x.UserId)
            .Select(g => new
            {
                UserId = g.Key,
                MessageCount = g.Sum(x => x.MessageCount),
                UniqueChannels = g.Max(x => x.UniqueChannelsActive),
                LastActive = g.Max(x => x.PeriodStart),
                User = g.FirstOrDefault()?.User
            })
            .OrderByDescending(x => x.MessageCount)
            .Take(limit)
            .Select(x => new TopContributorDto
            {
                UserId = x.UserId,
                Username = x.User?.Username ?? "Unknown",
                DisplayName = x.User?.GlobalDisplayName,
                AvatarUrl = x.User?.AvatarHash != null
                    ? $"https://cdn.discordapp.com/avatars/{x.UserId}/{x.User.AvatarHash}.png"
                    : null,
                MessageCount = x.MessageCount,
                UniqueChannels = x.UniqueChannels,
                LastActive = x.LastActive
            })
            .ToList();

        _logger.LogInformation(
            "Retrieved {Count} top contributors for guild {GuildId}",
            contributors.Count, guildId);

        return contributors;
    }
}
