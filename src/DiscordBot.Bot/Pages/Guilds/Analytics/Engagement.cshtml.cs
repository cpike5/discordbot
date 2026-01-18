using DiscordBot.Bot.Configuration;
using DiscordBot.Bot.ViewModels.Components;
using DiscordBot.Bot.ViewModels.Pages;
using DiscordBot.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DiscordBot.Bot.Pages.Guilds.Analytics;

/// <summary>
/// Page model for the Guild Engagement Analytics Dashboard.
/// Displays engagement metrics, message trends, channel analytics, and new member retention.
/// </summary>
[Authorize(Policy = "RequireViewer")]
[Authorize(Policy = "GuildAccess")]
public class EngagementModel : PageModel
{
    private readonly IEngagementAnalyticsService _engagementAnalyticsService;
    private readonly IGuildService _guildService;
    private readonly ILogger<EngagementModel> _logger;

    public EngagementModel(
        IEngagementAnalyticsService engagementAnalyticsService,
        IGuildService guildService,
        ILogger<EngagementModel> logger)
    {
        _engagementAnalyticsService = engagementAnalyticsService;
        _guildService = guildService;
        _logger = logger;
    }

    /// <summary>
    /// The analytics view model with all chart data and metrics.
    /// </summary>
    public EngagementAnalyticsViewModel ViewModel { get; private set; } = new();

    /// <summary>
    /// Guild layout breadcrumb ViewModel.
    /// </summary>
    public GuildBreadcrumbViewModel Breadcrumb { get; set; } = new();

    /// <summary>
    /// Guild layout header ViewModel.
    /// </summary>
    public GuildHeaderViewModel Header { get; set; } = new();

    /// <summary>
    /// Guild layout navigation ViewModel.
    /// </summary>
    public GuildNavBarViewModel Navigation { get; set; } = new();

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
    /// Handles GET requests to display the engagement analytics dashboard.
    /// </summary>
    /// <param name="guildId">The guild's Discord snowflake ID from route parameter.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The page result.</returns>
    public async Task<IActionResult> OnGetAsync(ulong guildId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Loading engagement analytics for guild {GuildId}", guildId);

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
            var summaryTask = _engagementAnalyticsService.GetSummaryAsync(guildId, start, end, cancellationToken);
            var messageTrendsTask = _engagementAnalyticsService.GetMessageTrendsAsync(guildId, start, end, cancellationToken);
            var newMemberRetentionTask = _engagementAnalyticsService.GetNewMemberRetentionAsync(guildId, start, end, cancellationToken);
            var channelEngagementTask = GetChannelEngagementAsync(guildId, start, end, cancellationToken);

            await Task.WhenAll(summaryTask, messageTrendsTask, newMemberRetentionTask, channelEngagementTask);

            var summary = await summaryTask;
            var messageTrends = await messageTrendsTask;
            var newMemberRetention = await newMemberRetentionTask;
            var channelEngagement = await channelEngagementTask;

            // Build view model
            ViewModel = new EngagementAnalyticsViewModel
            {
                GuildId = guildId,
                GuildName = guild.Name,
                GuildIconUrl = guild.IconUrl,
                Summary = summary,
                MessageTrends = messageTrends.ToList(),
                ChannelEngagement = channelEngagement,
                NewMemberRetention = newMemberRetention.ToList(),
                StartDate = start,
                EndDate = end.AddDays(-1) // Show the actual end date (not +1)
            };

            _logger.LogInformation(
                "Engagement analytics loaded successfully for guild {GuildId}. Total messages: {TotalMessages}, Active members: {ActiveMembers}",
                guildId, summary.TotalMessages, summary.ActiveMembers);

            // Populate guild layout ViewModels
            Breadcrumb = new GuildBreadcrumbViewModel
            {
                Items = new List<BreadcrumbItem>
                {
                    new() { Label = "Home", Url = "/" },
                    new() { Label = "Servers", Url = "/Guilds" },
                    new() { Label = guild.Name, Url = $"/Guilds/Details/{guildId}" },
                    new() { Label = "Analytics", Url = $"/Guilds/Analytics/{guildId}" },
                    new() { Label = "Engagement", IsCurrent = true }
                }
            };

            Header = new GuildHeaderViewModel
            {
                GuildId = guild.Id,
                GuildName = guild.Name,
                GuildIconUrl = guild.IconUrl,
                PageTitle = "Engagement Metrics",
                PageDescription = $"Message trends and member retention for {guild.Name}"
            };

            Navigation = new GuildNavBarViewModel
            {
                GuildId = guild.Id,
                ActiveTab = "overview",
                Tabs = GuildNavigationConfig.GetTabs().ToList()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load engagement analytics for guild {GuildId}", guildId);
            TempData["ErrorMessage"] = "Failed to load analytics data. Please try again.";
            // Return page with empty data
        }

        return Page();
    }

    /// <summary>
    /// Gets channel engagement metrics (placeholder implementation).
    /// This will be replaced with actual service call once channel tracking is implemented.
    /// </summary>
    /// <param name="guildId">Guild ID.</param>
    /// <param name="start">Start date.</param>
    /// <param name="end">End date.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of channel engagement metrics.</returns>
    private async Task<List<ChannelEngagementDto>> GetChannelEngagementAsync(
        ulong guildId,
        DateTime start,
        DateTime end,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("Retrieving channel engagement metrics for guild {GuildId}", guildId);

        // TODO: Implement actual channel engagement tracking
        // For now, return empty list - this will be populated once channel message tracking is added
        await Task.CompletedTask;
        return new List<ChannelEngagementDto>();
    }
}
