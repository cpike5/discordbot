using Discord.WebSocket;
using DiscordBot.Bot.ViewModels.Pages;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DiscordBot.Bot.Pages.Guilds.Analytics;

/// <summary>
/// Page model for the Server Analytics Dashboard.
/// Displays activity metrics, charts, and leaderboards for a specific guild.
/// </summary>
[Authorize(Policy = "RequireViewer")]
public class IndexModel : PageModel
{
    private readonly IServerAnalyticsService _analyticsService;
    private readonly IGuildService _guildService;
    private readonly IMessageLogRepository _messageLogRepository;
    private readonly DiscordSocketClient _discordClient;
    private readonly ILogger<IndexModel> _logger;

    public IndexModel(
        IServerAnalyticsService analyticsService,
        IGuildService guildService,
        IMessageLogRepository messageLogRepository,
        DiscordSocketClient discordClient,
        ILogger<IndexModel> logger)
    {
        _analyticsService = analyticsService;
        _guildService = guildService;
        _messageLogRepository = messageLogRepository;
        _discordClient = discordClient;
        _logger = logger;
    }

    /// <summary>
    /// The analytics view model with all chart data and metrics.
    /// </summary>
    public ServerAnalyticsViewModel ViewModel { get; private set; } = new();

    /// <summary>
    /// Start date filter (bound from query string).
    /// </summary>
    [BindProperty(SupportsGet = true)]
    public DateTime? StartDate { get; set; }

    /// <summary>
    /// End date filter (bound from query string).
    /// </summary>
    [BindProperty(SupportsGet = true)]
    public DateTime? EndDate { get; set; }

    /// <summary>
    /// Handles GET requests to display the analytics dashboard.
    /// </summary>
    /// <param name="guildId">The guild's Discord snowflake ID from route parameter.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The page result.</returns>
    public async Task<IActionResult> OnGetAsync(ulong guildId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Loading Server Analytics for guild {GuildId}", guildId);

        // Set defaults if not specified (normalize to midnight UTC)
        StartDate = StartDate.HasValue ? StartDate.Value.Date : DateTime.UtcNow.AddDays(-7).Date;
        EndDate = EndDate.HasValue ? EndDate.Value.Date : DateTime.UtcNow.Date;

        // Query range: start at beginning of StartDate (00:00:00) and end at beginning of day after EndDate (00:00:00)
        // This ensures we include all records from the entire EndDate day (up to 23:59:59.999)
        var start = StartDate.Value;
        var end = EndDate.Value.AddDays(1);

        _logger.LogDebug("Analytics date range: {Start} to {End}", start, end);

        try
        {
            // Get guild info
            var guild = await _guildService.GetGuildByIdAsync(guildId, cancellationToken);
            if (guild == null)
            {
                _logger.LogWarning("Guild {GuildId} not found", guildId);
                return NotFound();
            }

            // Load analytics data in parallel
            var summaryTask = _analyticsService.GetSummaryAsync(guildId, start, end, cancellationToken);
            var activityTimeSeriesTask = _analyticsService.GetActivityTimeSeriesAsync(guildId, start, end, cancellationToken);
            var activityHeatmapTask = _analyticsService.GetActivityHeatmapAsync(guildId, start, end, cancellationToken);
            var topContributorsTask = _analyticsService.GetTopContributorsAsync(guildId, start, end, 10, cancellationToken);
            var topChannelsTask = GetTopChannelsAsync(guildId, start, end, 10, cancellationToken);

            await Task.WhenAll(summaryTask, activityTimeSeriesTask, activityHeatmapTask, topContributorsTask, topChannelsTask);

            var summary = await summaryTask;
            var activityTimeSeries = await activityTimeSeriesTask;
            var activityHeatmap = await activityHeatmapTask;
            var topContributors = await topContributorsTask;
            var topChannels = await topChannelsTask;

            // Build view model
            ViewModel = new ServerAnalyticsViewModel
            {
                GuildId = guildId,
                GuildName = guild.Name,
                GuildIconUrl = guild.IconUrl,
                Summary = summary,
                ActivityTimeSeries = activityTimeSeries,
                ActivityHeatmap = activityHeatmap,
                TopContributors = topContributors,
                TopChannels = topChannels,
                StartDate = start,
                EndDate = end.AddDays(-1) // Show the actual end date (not +1)
            };

            _logger.LogInformation(
                "Analytics loaded successfully for guild {GuildId}. Total members: {TotalMembers}, Messages (7d): {Messages7d}",
                guildId, summary.TotalMembers, summary.Messages7d);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load analytics data for guild {GuildId}", guildId);
            TempData["ErrorMessage"] = "Failed to load analytics data. Please try again.";
            // Return page with empty data
        }

        return Page();
    }

    /// <summary>
    /// Gets the top channels by message count for the specified time period.
    /// </summary>
    /// <param name="guildId">Guild ID.</param>
    /// <param name="start">Start date (inclusive).</param>
    /// <param name="end">End date (exclusive).</param>
    /// <param name="limit">Maximum number of channels to return.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of top channels with message counts.</returns>
    private async Task<List<TopChannelDto>> GetTopChannelsAsync(
        ulong guildId,
        DateTime start,
        DateTime end,
        int limit,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("Retrieving top {Limit} channels for guild {GuildId}", limit, guildId);

        // Get all message logs for the guild in the time period
        var allLogs = await _messageLogRepository.GetAllAsync(cancellationToken);

        var topChannels = allLogs
            .Where(m => m.GuildId == guildId && m.Timestamp >= start && m.Timestamp < end)
            .GroupBy(m => m.ChannelId)
            .Select(g => new TopChannelDto
            {
                ChannelId = g.Key,
                ChannelName = string.Empty, // Will be populated below
                MessageCount = g.LongCount(),
                UniqueContributors = g.Select(m => m.AuthorId).Distinct().Count(),
                LastActivity = g.Max(m => m.Timestamp)
            })
            .OrderByDescending(c => c.MessageCount)
            .Take(limit)
            .ToList();

        // Resolve channel names
        var resolvedChannels = new List<TopChannelDto>();
        foreach (var channel in topChannels)
        {
            var channelName = await GetChannelNameAsync(channel.ChannelId, guildId);
            resolvedChannels.Add(channel with { ChannelName = channelName });
        }

        _logger.LogDebug("Retrieved {Count} top channels", resolvedChannels.Count);
        return resolvedChannels;
    }

    /// <summary>
    /// Gets the display name for a Discord channel in a guild.
    /// </summary>
    private async Task<string> GetChannelNameAsync(ulong channelId, ulong guildId)
    {
        try
        {
            var guild = _discordClient.GetGuild(guildId);
            if (guild == null)
            {
                _logger.LogWarning("Guild {GuildId} not found when resolving channel name for channel {ChannelId}", guildId, channelId);
                return "Unknown Channel";
            }

            var channel = guild.GetChannel(channelId);
            if (channel != null)
            {
                return $"#{channel.Name}";
            }

            // Try downloading channels if not in cache
            await guild.DownloadUsersAsync(); // This also refreshes channel cache
            channel = guild.GetChannel(channelId);
            if (channel != null)
            {
                return $"#{channel.Name}";
            }

            _logger.LogDebug("Channel {ChannelId} not found in guild {GuildId}", channelId, guildId);
            return "Unknown Channel";
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get channel name for channel {ChannelId} in guild {GuildId}", channelId, guildId);
            return "Unknown Channel";
        }
    }
}
