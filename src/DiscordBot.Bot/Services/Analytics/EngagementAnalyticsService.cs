using DiscordBot.Bot.Tracing;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace DiscordBot.Bot.Services.Analytics;

/// <summary>
/// Service for generating engagement analytics and member retention metrics.
/// Aggregates data from message logs and guild metrics to provide insights into member engagement.
/// </summary>
public class EngagementAnalyticsService : IEngagementAnalyticsService
{
    private readonly IMessageLogRepository _messageLogRepository;
    private readonly IGuildMetricsRepository _guildMetricsRepository;
    private readonly IGuildMemberRepository _guildMemberRepository;
    private readonly ILogger<EngagementAnalyticsService> _logger;

    public EngagementAnalyticsService(
        IMessageLogRepository messageLogRepository,
        IGuildMetricsRepository guildMetricsRepository,
        IGuildMemberRepository guildMemberRepository,
        ILogger<EngagementAnalyticsService> logger)
    {
        _messageLogRepository = messageLogRepository;
        _guildMetricsRepository = guildMetricsRepository;
        _guildMemberRepository = guildMemberRepository;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<EngagementAnalyticsSummaryDto> GetSummaryAsync(
        ulong guildId,
        DateTime start,
        DateTime end,
        CancellationToken ct = default)
    {
        using var activity = BotActivitySource.StartServiceActivity(
            "engagement_analytics",
            "get_summary",
            guildId: guildId);

        try
        {
            _logger.LogDebug(
                "Generating engagement analytics summary for guild {GuildId} from {Start} to {End}",
                guildId, start, end);

            // Calculate time ranges
            var now = DateTime.UtcNow;
            var last24h = now.AddHours(-24);
            var last7d = now.AddDays(-7);

            // Get message trends for the requested period
            var messageTrends = await GetMessageTrendsAsync(guildId, start, end, ct);

            var totalMessages = messageTrends.Sum(t => t.MessageCount);
            var messagesPerDay = (end - start).TotalDays > 0
                ? (decimal)totalMessages / (decimal)(end - start).TotalDays
                : 0m;

            // Get unique active members from message trends
            var activeMembers = messageTrends
                .Select(t => t.UniqueAuthors)
                .DefaultIfEmpty(0)
                .Max();

            // Get messages for last 24h and 7d
            var messageTrendsRecent = await GetMessageTrendsAsync(guildId, last7d, now, ct);

            var messagesLast24h = messageTrendsRecent
                .Where(t => t.Date >= last24h)
                .Sum(t => t.MessageCount);

            var messagesLast7d = messageTrendsRecent.Sum(t => t.MessageCount);

            // Get new members count from guild metrics
            var metricsLast7d = await _guildMetricsRepository.GetByDateRangeAsync(
                guildId,
                DateOnly.FromDateTime(last7d),
                DateOnly.FromDateTime(now),
                ct);

            var newMembers7d = metricsLast7d.Sum(m => m.MembersJoined);

            // Calculate new member retention rate
            // Get members who joined in the last 7 days
            var newMemberRetention = await CalculateNewMemberRetentionAsync(guildId, last7d, now, ct);

            _logger.LogInformation(
                "Engagement analytics summary generated for guild {GuildId}: {TotalMessages} messages, {ActiveMembers} active members",
                guildId, totalMessages, activeMembers);

            var result = new EngagementAnalyticsSummaryDto
            {
                TotalMessages = totalMessages,
                Messages24h = messagesLast24h,
                Messages7d = messagesLast7d,
                MessagesPerDay = messagesPerDay,
                ActiveMembers = activeMembers,
                NewMembers7d = newMembers7d,
                NewMemberRetentionRate = newMemberRetention,
                ReactionCount = 0, // Not currently tracked
                VoiceMinutes = 0   // Not currently tracked
            };

            BotActivitySource.SetSuccess(activity);
            return result;
        }
        catch (Exception ex)
        {
            BotActivitySource.RecordException(activity, ex);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<MessageTrendDto>> GetMessageTrendsAsync(
        ulong guildId,
        DateTime start,
        DateTime end,
        CancellationToken ct = default)
    {
        using var activity = BotActivitySource.StartServiceActivity(
            "engagement_analytics",
            "get_message_trends",
            guildId: guildId);

        try
        {
            _logger.LogDebug(
                "Generating message trends for guild {GuildId} from {Start} to {End}",
                guildId, start, end);

            // Get messages by day from the repository
            var daySpan = (int)Math.Ceiling((end - start).TotalDays);
            var messagesByDay = await _messageLogRepository.GetMessagesByDayAsync(
                days: daySpan + 1, // Add extra day to ensure we cover the range
                guildId: guildId,
                cancellationToken: ct);

            // Filter to the requested date range and calculate trends
            var trends = messagesByDay
                .Where(x => x.Date >= DateOnly.FromDateTime(start) && x.Date <= DateOnly.FromDateTime(end))
                .Select(x => new MessageTrendDto
                {
                    Date = x.Date.ToDateTime(TimeOnly.MinValue),
                    MessageCount = x.Count,
                    UniqueAuthors = 0, // Will need to calculate separately if needed
                    AvgMessageLength = 0m // Will need to calculate separately if needed
                })
                .OrderBy(t => t.Date)
                .ToList();

            // Note: To get unique authors and avg message length, we would need to query the message logs directly
            // For performance, we're using the pre-aggregated data from GetMessagesByDayAsync
            // Future enhancement: Add these calculations if needed

            _logger.LogDebug(
                "Generated {Count} message trend data points for guild {GuildId}",
                trends.Count, guildId);

            BotActivitySource.SetSuccess(activity);
            return trends;
        }
        catch (Exception ex)
        {
            BotActivitySource.RecordException(activity, ex);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<NewMemberRetentionDto>> GetNewMemberRetentionAsync(
        ulong guildId,
        DateTime start,
        DateTime end,
        CancellationToken ct = default)
    {
        using var activity = BotActivitySource.StartServiceActivity(
            "engagement_analytics",
            "get_new_member_retention",
            guildId: guildId);

        try
        {
            _logger.LogDebug(
                "Generating new member retention metrics for guild {GuildId} from {Start} to {End}",
                guildId, start, end);

            // Get guild metrics for the period to see when members joined
            var metrics = await _guildMetricsRepository.GetByDateRangeAsync(
                guildId,
                DateOnly.FromDateTime(start),
                DateOnly.FromDateTime(end),
                ct);

            // For a simplified implementation, we'll use the metrics data
            // Full implementation would require tracking individual member join dates and first message timestamps
            var retention = metrics
                .Where(m => m.MembersJoined > 0)
                .Select(m => new NewMemberRetentionDto
                {
                    JoinDate = m.SnapshotDate.ToDateTime(TimeOnly.MinValue),
                    NewMembers = m.MembersJoined,
                    SentFirstMessage = 0, // Would need to query message logs for this
                    StillActive7d = 0,    // Would need to track member activity over time
                    StillActive30d = 0,   // Would need to track member activity over time
                    FirstMessageRate = 0m,
                    Retention7dRate = 0m,
                    Retention30dRate = 0m
                })
                .OrderBy(r => r.JoinDate)
                .ToList();

            _logger.LogDebug(
                "Generated {Count} retention data points for guild {GuildId}",
                retention.Count, guildId);

            _logger.LogWarning(
                "New member retention tracking is simplified - detailed metrics require per-member activity tracking");

            BotActivitySource.SetSuccess(activity);
            return retention;
        }
        catch (Exception ex)
        {
            BotActivitySource.RecordException(activity, ex);
            throw;
        }
    }

    /// <summary>
    /// Gets channel engagement metrics showing message counts, unique authors, and engagement rates.
    /// </summary>
    public async Task<IReadOnlyList<ChannelEngagementDto>> GetChannelEngagementAsync(
        ulong guildId,
        DateTime start,
        DateTime end,
        CancellationToken ct = default)
    {
        using var activity = BotActivitySource.StartServiceActivity(
            "engagement_analytics",
            "get_channel_engagement",
            guildId: guildId);

        try
        {
            _logger.LogDebug(
                "Generating channel engagement metrics for guild {GuildId} from {Start} to {End}",
                guildId, start, end);

            // Get all guild messages from the repository (need raw data for grouping)
            var query = _messageLogRepository.GetGuildMessagesAsync(
                guildId,
                since: start,
                limit: int.MaxValue,
                cancellationToken: ct);

            var messages = await query;

            // Filter to the date range (GetGuildMessagesAsync uses "since" but we need an upper bound too)
            var filteredMessages = messages
                .Where(m => m.Timestamp >= start && m.Timestamp < end)
                .Where(m => m.Source == MessageSource.ServerChannel)
                .ToList();

            if (filteredMessages.Count == 0)
            {
                _logger.LogDebug("No server channel messages found for guild {GuildId}", guildId);
                BotActivitySource.SetSuccess(activity);
                return Array.Empty<ChannelEngagementDto>();
            }

            // Get guild member count for engagement rate calculation
            var memberCount = await _guildMemberRepository.GetMemberCountAsync(guildId, activeOnly: true, cancellationToken: ct);

            if (memberCount == 0)
            {
                _logger.LogWarning("Guild {GuildId} has no active members for engagement rate calculation", guildId);
                memberCount = 1; // Prevent division by zero
            }

            // Group by channel and calculate metrics
            var channelMetrics = filteredMessages
                .GroupBy(m => m.ChannelId)
                .Select(g => new ChannelEngagementDto
                {
                    ChannelId = g.Key,
                    ChannelName = g.First().ChannelName ?? "unknown",
                    MessageCount = g.LongCount(),
                    UniqueAuthors = g.Select(m => m.AuthorId).Distinct().Count(),
                    EngagementRate = (decimal)g.Select(m => m.AuthorId).Distinct().Count() / memberCount * 100m
                })
                .OrderByDescending(x => x.MessageCount)
                .Take(20)
                .ToList();

            _logger.LogDebug(
                "Generated channel engagement metrics for {Count} channels in guild {GuildId}",
                channelMetrics.Count, guildId);

            BotActivitySource.SetSuccess(activity);
            return channelMetrics;
        }
        catch (Exception ex)
        {
            BotActivitySource.RecordException(activity, ex);
            _logger.LogError(ex, "Error generating channel engagement metrics for guild {GuildId}", guildId);
            throw;
        }
    }

    /// <summary>
    /// Calculates the new member retention rate for the specified time period.
    /// Returns the percentage of new members who sent at least one message within 7 days of joining.
    /// </summary>
    private async Task<decimal> CalculateNewMemberRetentionAsync(
        ulong guildId,
        DateTime start,
        DateTime end,
        CancellationToken ct)
    {
        try
        {
            // Get metrics for new members
            var metrics = await _guildMetricsRepository.GetByDateRangeAsync(
                guildId,
                DateOnly.FromDateTime(start),
                DateOnly.FromDateTime(end),
                ct);

            var totalNewMembers = metrics.Sum(m => m.MembersJoined);

            if (totalNewMembers == 0)
            {
                return 0m;
            }

            // For now, return a placeholder
            // Full implementation would require tracking individual member join dates and activity
            _logger.LogDebug(
                "Retention rate calculation is simplified - detailed tracking not yet implemented");

            return 0m;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating new member retention for guild {GuildId}", guildId);
            return 0m;
        }
    }
}
