using DiscordBot.Bot.Extensions;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DiscordBot.Bot.Controllers;

/// <summary>
/// Controller for guild analytics data.
/// Provides endpoints for retrieving summary stats, activity time series,
/// channel rankings, and member growth metrics.
/// </summary>
[ApiController]
[Route("api/analytics")]
[Authorize(Policy = "RequireViewer")]
public class AnalyticsController : ControllerBase
{
    private readonly IMemberActivityRepository _memberActivityRepo;
    private readonly IChannelActivityRepository _channelActivityRepo;
    private readonly IGuildMetricsRepository _guildMetricsRepo;
    private readonly IGuildRepository _guildRepository;
    private readonly ILogger<AnalyticsController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AnalyticsController"/> class.
    /// </summary>
    /// <param name="memberActivityRepo">The member activity repository.</param>
    /// <param name="channelActivityRepo">The channel activity repository.</param>
    /// <param name="guildMetricsRepo">The guild metrics repository.</param>
    /// <param name="guildRepository">The guild repository.</param>
    /// <param name="logger">The logger.</param>
    public AnalyticsController(
        IMemberActivityRepository memberActivityRepo,
        IChannelActivityRepository channelActivityRepo,
        IGuildMetricsRepository guildMetricsRepo,
        IGuildRepository guildRepository,
        ILogger<AnalyticsController> logger)
    {
        _memberActivityRepo = memberActivityRepo;
        _channelActivityRepo = channelActivityRepo;
        _guildMetricsRepo = guildMetricsRepo;
        _guildRepository = guildRepository;
        _logger = logger;
    }

    /// <summary>
    /// Gets a quick summary of guild analytics.
    /// </summary>
    /// <param name="guildId">Discord guild ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Summary statistics including 24h and 7d metrics.</returns>
    [HttpGet("{guildId}/summary")]
    [ProducesResponseType(typeof(GuildAnalyticsSummaryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<GuildAnalyticsSummaryDto>> GetSummary(
        ulong guildId,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("Analytics summary requested for guild {GuildId}", guildId);

        // Verify guild exists
        var guild = await _guildRepository.GetByIdAsync(guildId, cancellationToken);
        if (guild == null)
        {
            _logger.LogWarning("Guild {GuildId} not found", guildId);

            return NotFound(new ApiErrorDto
            {
                Message = "Guild not found",
                Detail = $"No guild with ID {guildId} exists in the database.",
                StatusCode = StatusCodes.Status404NotFound,
                TraceId = HttpContext.GetCorrelationId()
            });
        }

        // Get the latest snapshot
        var latestSnapshot = await _guildMetricsRepo.GetLatestAsync(guildId, cancellationToken);

        // Calculate date ranges for 24h and 7d
        var now = DateTime.UtcNow;
        var date24hAgo = now.AddHours(-24);
        var date7dAgo = now.AddDays(-7);

        // Get 24h activity time series (hourly granularity)
        var activity24h = await _memberActivityRepo.GetActivityTimeSeriesAsync(
            guildId,
            date24hAgo,
            now,
            SnapshotGranularity.Hourly,
            cancellationToken);

        // Get 7d metrics snapshots (daily granularity)
        var dateOnly7dAgo = DateOnly.FromDateTime(date7dAgo);
        var dateOnlyToday = DateOnly.FromDateTime(now);
        var metrics7d = await _guildMetricsRepo.GetByDateRangeAsync(
            guildId,
            dateOnly7dAgo,
            dateOnlyToday,
            cancellationToken);

        // Get top channel for the last 7 days
        var topChannels = await _channelActivityRepo.GetChannelRankingsAsync(
            guildId,
            date7dAgo,
            now,
            limit: 1,
            cancellationToken);

        // Aggregate metrics from snapshots
        var messages24h = activity24h.Sum(x => x.TotalMessages);
        var activeMembers24h = activity24h.Any() ? activity24h.Max(x => x.ActiveMembers) : 0;

        var messages7d = metrics7d.Sum(x => x.TotalMessages);
        var activeMembers7d = metrics7d.Any() ? metrics7d.Max(x => x.ActiveMembers) : 0;
        var memberGrowth7d = metrics7d.Sum(x => x.MembersJoined - x.MembersLeft);
        var commands24h = metrics7d
            .Where(x => x.SnapshotDate >= dateOnly7dAgo.AddDays(6)) // Last day only
            .Sum(x => x.CommandsExecuted);
        var moderationActions7d = metrics7d.Sum(x => x.ModerationActions);

        var topChannel = topChannels.FirstOrDefault();

        var summary = new GuildAnalyticsSummaryDto
        {
            TotalMembers = latestSnapshot?.TotalMembers ?? 0,
            ActiveMembers24h = activeMembers24h,
            ActiveMembers7d = activeMembers7d,
            Messages24h = messages24h,
            Messages7d = messages7d,
            MemberGrowth7d = memberGrowth7d,
            Commands24h = commands24h,
            ModerationActions7d = moderationActions7d,
            TopChannelId = topChannel?.ChannelId,
            TopChannelName = topChannel?.ChannelName
        };

        _logger.LogTrace(
            "Analytics summary for guild {GuildId}: {TotalMembers} total members, {Messages24h} messages (24h), {Messages7d} messages (7d)",
            guildId, summary.TotalMembers, summary.Messages24h, summary.Messages7d);

        return Ok(summary);
    }

    /// <summary>
    /// Gets member activity time series data.
    /// </summary>
    /// <param name="guildId">Discord guild ID.</param>
    /// <param name="start">Optional start date (default: 7 days ago).</param>
    /// <param name="end">Optional end date (default: now).</param>
    /// <param name="granularity">Optional granularity (hourly or daily, default: hourly).</param>
    /// <param name="limit">Maximum number of results (default: 100).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of member activity data points.</returns>
    [HttpGet("{guildId}/activity")]
    [ProducesResponseType(typeof(IEnumerable<MemberActivityDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IEnumerable<MemberActivityDto>>> GetActivity(
        ulong guildId,
        [FromQuery] DateTime? start = null,
        [FromQuery] DateTime? end = null,
        [FromQuery] string? granularity = null,
        [FromQuery] int limit = 100,
        CancellationToken cancellationToken = default)
    {
        var startDate = start ?? DateTime.UtcNow.AddDays(-7);
        var endDate = end ?? DateTime.UtcNow;

        _logger.LogDebug("Activity data requested for guild {GuildId}, {StartDate} to {EndDate}, granularity: {Granularity}",
            guildId, startDate, endDate, granularity ?? "hourly");

        // Validate date range
        if (startDate > endDate)
        {
            _logger.LogWarning("Invalid date range for guild {GuildId}: StartDate={StartDate} > EndDate={EndDate}",
                guildId, startDate, endDate);

            return BadRequest(new ApiErrorDto
            {
                Message = "Invalid date range",
                Detail = "Start date cannot be after end date.",
                StatusCode = StatusCodes.Status400BadRequest,
                TraceId = HttpContext.GetCorrelationId()
            });
        }

        // Parse granularity (currently not used in this endpoint but validated for future use)
        if (!string.IsNullOrEmpty(granularity))
        {
            if (granularity.Equals("daily", StringComparison.OrdinalIgnoreCase))
            {
                // Daily granularity - validated but not currently used
            }
            else if (!granularity.Equals("hourly", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("Invalid granularity value: {Granularity}", granularity);

                return BadRequest(new ApiErrorDto
                {
                    Message = "Invalid granularity",
                    Detail = "Granularity must be either 'hourly' or 'daily'.",
                    StatusCode = StatusCodes.Status400BadRequest,
                    TraceId = HttpContext.GetCorrelationId()
                });
            }
        }

        // Get top active members for the period
        var topMembers = await _memberActivityRepo.GetTopActiveMembersAsync(
            guildId,
            startDate,
            endDate,
            limit,
            cancellationToken);

        // Map to DTOs
        var activityDtos = topMembers.Select(snapshot => new MemberActivityDto
        {
            Period = snapshot.PeriodStart,
            UserId = snapshot.UserId,
            Username = snapshot.User?.Username,
            DisplayName = snapshot.User?.GlobalDisplayName ?? snapshot.User?.Username,
            MessageCount = snapshot.MessageCount,
            ReactionCount = snapshot.ReactionCount,
            VoiceMinutes = snapshot.VoiceMinutes,
            UniqueChannels = snapshot.UniqueChannelsActive
        }).ToList();

        _logger.LogTrace("Retrieved {Count} member activity records for guild {GuildId}",
            activityDtos.Count, guildId);

        return Ok(activityDtos);
    }

    /// <summary>
    /// Gets channel activity rankings.
    /// </summary>
    /// <param name="guildId">Discord guild ID.</param>
    /// <param name="start">Optional start date (default: 7 days ago).</param>
    /// <param name="end">Optional end date (default: now).</param>
    /// <param name="limit">Maximum number of channels to return (default: 10).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of top channels ordered by message count.</returns>
    [HttpGet("{guildId}/channels")]
    [ProducesResponseType(typeof(IEnumerable<ChannelActivityDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<ChannelActivityDto>>> GetChannelActivity(
        ulong guildId,
        [FromQuery] DateTime? start = null,
        [FromQuery] DateTime? end = null,
        [FromQuery] int limit = 10,
        CancellationToken cancellationToken = default)
    {
        var startDate = start ?? DateTime.UtcNow.AddDays(-7);
        var endDate = end ?? DateTime.UtcNow;

        _logger.LogDebug("Channel activity requested for guild {GuildId}, {StartDate} to {EndDate}, limit: {Limit}",
            guildId, startDate, endDate, limit);

        var channelRankings = await _channelActivityRepo.GetChannelRankingsAsync(
            guildId,
            startDate,
            endDate,
            limit,
            cancellationToken);

        // Map to DTOs
        var channelDtos = channelRankings.Select(snapshot => new ChannelActivityDto
        {
            ChannelId = snapshot.ChannelId,
            ChannelName = snapshot.ChannelName,
            MessageCount = snapshot.MessageCount,
            UniqueUsers = snapshot.UniqueUsers,
            PeakHour = snapshot.PeakHour,
            AverageMessageLength = snapshot.AverageMessageLength
        }).ToList();

        _logger.LogTrace("Retrieved {Count} channel activity records for guild {GuildId}",
            channelDtos.Count, guildId);

        return Ok(channelDtos);
    }

    /// <summary>
    /// Gets member growth metrics over time.
    /// </summary>
    /// <param name="guildId">Discord guild ID.</param>
    /// <param name="start">Optional start date (default: 30 days ago).</param>
    /// <param name="end">Optional end date (default: today).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of daily growth data points.</returns>
    [HttpGet("{guildId}/growth")]
    [ProducesResponseType(typeof(IEnumerable<GuildGrowthDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IEnumerable<GuildGrowthDto>>> GetGrowth(
        ulong guildId,
        [FromQuery] DateOnly? start = null,
        [FromQuery] DateOnly? end = null,
        CancellationToken cancellationToken = default)
    {
        var startDate = start ?? DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-30));
        var endDate = end ?? DateOnly.FromDateTime(DateTime.UtcNow);

        _logger.LogDebug("Growth data requested for guild {GuildId}, {StartDate} to {EndDate}",
            guildId, startDate, endDate);

        // Validate date range
        if (startDate > endDate)
        {
            _logger.LogWarning("Invalid date range for guild {GuildId}: StartDate={StartDate} > EndDate={EndDate}",
                guildId, startDate, endDate);

            return BadRequest(new ApiErrorDto
            {
                Message = "Invalid date range",
                Detail = "Start date cannot be after end date.",
                StatusCode = StatusCodes.Status400BadRequest,
                TraceId = HttpContext.GetCorrelationId()
            });
        }

        // Get growth time series
        var growthData = await _guildMetricsRepo.GetGrowthTimeSeriesAsync(
            guildId,
            startDate,
            endDate,
            cancellationToken);

        // Get full metrics snapshots to include total member count
        var metricsSnapshots = await _guildMetricsRepo.GetByDateRangeAsync(
            guildId,
            startDate,
            endDate,
            cancellationToken);

        // Create a lookup for total member counts
        var totalMembersLookup = metricsSnapshots.ToDictionary(
            m => m.SnapshotDate,
            m => m.TotalMembers);

        // Map to DTOs
        var growthDtos = growthData.Select(data => new GuildGrowthDto
        {
            Date = data.Date,
            TotalMembers = totalMembersLookup.TryGetValue(data.Date, out var total) ? total : 0,
            Joined = data.Joined,
            Left = data.Left,
            NetGrowth = data.NetGrowth
        }).ToList();

        _logger.LogTrace("Retrieved {Count} growth data points for guild {GuildId}",
            growthDtos.Count, guildId);

        return Ok(growthDtos);
    }
}
