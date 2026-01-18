using Discord.WebSocket;
using DiscordBot.Bot.Configuration;
using DiscordBot.Bot.ViewModels.Components;
using DiscordBot.Bot.ViewModels.Pages;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DiscordBot.Bot.Pages.Guilds.RatWatch;

/// <summary>
/// Page model for the Guild Rat Watch Analytics Dashboard.
/// Displays analytics metrics, charts, and leaderboards for a specific guild.
/// </summary>
[Authorize(Policy = "RequireModerator")]
[Authorize(Policy = "GuildAccess")]
public class AnalyticsModel : PageModel
{
    private readonly IRatWatchRepository _ratWatchRepository;
    private readonly IRatRecordRepository _ratRecordRepository;
    private readonly IGuildService _guildService;
    private readonly DiscordSocketClient _discordClient;
    private readonly ILogger<AnalyticsModel> _logger;

    public AnalyticsModel(
        IRatWatchRepository ratWatchRepository,
        IRatRecordRepository ratRecordRepository,
        IGuildService guildService,
        DiscordSocketClient discordClient,
        ILogger<AnalyticsModel> logger)
    {
        _ratWatchRepository = ratWatchRepository;
        _ratRecordRepository = ratRecordRepository;
        _guildService = guildService;
        _discordClient = discordClient;
        _logger = logger;
    }

    /// <summary>
    /// The analytics view model with all chart data and metrics.
    /// </summary>
    public RatWatchAnalyticsViewModel ViewModel { get; private set; } = new();

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
    /// Handles GET requests to display the analytics dashboard.
    /// </summary>
    /// <param name="guildId">The guild's Discord snowflake ID from route parameter.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The page result.</returns>
    public async Task<IActionResult> OnGetAsync(ulong guildId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Loading Rat Watch analytics for guild {GuildId}", guildId);

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

            // Load analytics data and leaderboards in parallel
            var summaryTask = _ratWatchRepository.GetAnalyticsSummaryAsync(guildId, start, end, cancellationToken);
            var timeSeriesTask = _ratWatchRepository.GetTimeSeriesAsync(guildId, start, end, cancellationToken);
            var heatmapTask = _ratWatchRepository.GetActivityHeatmapAsync(guildId, start, end, cancellationToken);
            var mostWatchedTask = _ratRecordRepository.GetUserMetricsAsync(guildId, "watched", 10, cancellationToken);
            var biggestRatsTask = _ratRecordRepository.GetUserMetricsAsync(guildId, "guilty", 10, cancellationToken);
            var topAccusersTask = GetTopAccusersAsync(guildId, 10, cancellationToken);

            await Task.WhenAll(summaryTask, timeSeriesTask, heatmapTask, mostWatchedTask, biggestRatsTask, topAccusersTask);

            var summary = await summaryTask;
            var timeSeries = await timeSeriesTask;
            var heatmap = await heatmapTask;
            var mostWatched = await mostWatchedTask;
            var biggestRats = await biggestRatsTask;
            var topAccusers = await topAccusersTask;

            // Resolve usernames for all leaderboard entries in parallel
            var mostWatchedNamesTask = ResolveUsernamesAsync(guildId, mostWatched);
            var biggestRatsNamesTask = ResolveUsernamesAsync(guildId, biggestRats);
            var topAccusersNamesTask = ResolveAccuserUsernamesAsync(guildId, topAccusers);

            await Task.WhenAll(mostWatchedNamesTask, biggestRatsNamesTask, topAccusersNamesTask);

            var mostWatchedWithNames = await mostWatchedNamesTask;
            var biggestRatsWithNames = await biggestRatsNamesTask;
            var topAccusersWithNames = await topAccusersNamesTask;

            // Build view model
            ViewModel = new RatWatchAnalyticsViewModel
            {
                GuildId = guildId,
                GuildName = guild.Name,
                GuildIconUrl = guild.IconUrl,
                Summary = summary,
                TimeSeries = timeSeries.ToList(),
                Heatmap = heatmap.ToList(),
                MostWatched = mostWatchedWithNames,
                TopAccusers = topAccusersWithNames,
                BiggestRats = biggestRatsWithNames,
                StartDate = start,
                EndDate = end.AddDays(-1) // Show the actual end date (not +1)
            };

            _logger.LogInformation(
                "Analytics loaded successfully for guild {GuildId}. Total watches: {TotalWatches}, Guilty rate: {GuiltyRate:F2}%",
                guildId, summary.TotalWatches, summary.GuiltyRate);

            // Populate guild layout ViewModels
            Breadcrumb = new GuildBreadcrumbViewModel
            {
                Items = new List<BreadcrumbItem>
                {
                    new() { Label = "Home", Url = "/" },
                    new() { Label = "Servers", Url = "/Guilds" },
                    new() { Label = guild.Name, Url = $"/Guilds/Details/{guildId}" },
                    new() { Label = "Rat Watch", Url = $"/Guilds/RatWatch/{guildId}" },
                    new() { Label = "Analytics", IsCurrent = true }
                }
            };

            Header = new GuildHeaderViewModel
            {
                GuildId = guild.Id,
                GuildName = guild.Name,
                GuildIconUrl = guild.IconUrl,
                PageTitle = "Rat Watch Analytics",
                PageDescription = $"Usage metrics and accountability trends for {guild.Name}"
            };

            Navigation = new GuildNavBarViewModel
            {
                GuildId = guild.Id,
                ActiveTab = "ratwatch",
                Tabs = GuildNavigationConfig.GetTabs().ToList()
            };
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
    /// Gets the top accusers (users who created the most watches).
    /// </summary>
    /// <param name="guildId">Guild ID.</param>
    /// <param name="limit">Maximum number of entries to return.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of accuser metrics.</returns>
    private async Task<List<AccuserMetricsDto>> GetTopAccusersAsync(
        ulong guildId,
        int limit,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("Retrieving top {Limit} accusers for guild {GuildId}", limit, guildId);

        // Get all watches for the guild and group by initiator
        var accuserMetrics = await _ratWatchRepository.GetAllAsync(cancellationToken);

        var topAccusers = accuserMetrics
            .Where(w => w.GuildId == guildId)
            .GroupBy(w => w.InitiatorUserId)
            .Select(g => new AccuserMetricsDto
            {
                UserId = g.Key,
                Username = string.Empty, // Will be populated by view if needed
                WatchesCreated = g.Count(),
                GuiltyCount = g.Count(w => w.Status == Core.Enums.RatWatchStatus.Guilty),
                SuccessRate = g.Any() ? (double)g.Count(w => w.Status == Core.Enums.RatWatchStatus.Guilty) / g.Count() * 100 : 0,
                LastCreatedDate = g.Max(w => w.CreatedAt)
            })
            .OrderByDescending(a => a.WatchesCreated)
            .Take(limit)
            .ToList();

        _logger.LogDebug("Retrieved {Count} top accusers", topAccusers.Count);
        return topAccusers;
    }

    /// <summary>
    /// Resolves usernames for user metrics entries in parallel.
    /// </summary>
    private async Task<List<RatWatchUserMetricsDto>> ResolveUsernamesAsync(
        ulong guildId,
        IEnumerable<RatWatchUserMetricsDto> metrics)
    {
        var metricsList = metrics.ToList();
        var tasks = metricsList.Select(async metric =>
        {
            var username = await GetUsernameAsync(metric.UserId, guildId);
            return metric with { Username = username };
        });

        var results = await Task.WhenAll(tasks);
        return results.ToList();
    }

    /// <summary>
    /// Resolves usernames for accuser metrics entries in parallel.
    /// </summary>
    private async Task<List<AccuserMetricsDto>> ResolveAccuserUsernamesAsync(
        ulong guildId,
        IEnumerable<AccuserMetricsDto> metrics)
    {
        var metricsList = metrics.ToList();
        var tasks = metricsList.Select(async metric =>
        {
            var username = await GetUsernameAsync(metric.UserId, guildId);
            return metric with { Username = username };
        });

        var results = await Task.WhenAll(tasks);
        return results.ToList();
    }

    /// <summary>
    /// Gets the display name for a Discord user in a guild.
    /// </summary>
    private async Task<string> GetUsernameAsync(ulong userId, ulong guildId)
    {
        try
        {
            var guild = _discordClient.GetGuild(guildId);
            if (guild == null)
            {
                _logger.LogWarning("Guild {GuildId} not found when resolving username for user {UserId}", guildId, userId);
                return "Unknown User";
            }

            var user = guild.GetUser(userId);
            if (user != null)
            {
                return user.DisplayName;
            }

            // Try downloading users if not in cache
            if (!guild.HasAllMembers)
            {
                await guild.DownloadUsersAsync();
                user = guild.GetUser(userId);
                if (user != null)
                {
                    return user.DisplayName;
                }
            }

            _logger.LogDebug("User {UserId} not found in guild {GuildId}", userId, guildId);
            return "Unknown User";
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get username for user {UserId} in guild {GuildId}", userId, guildId);
            return "Unknown User";
        }
    }
}
