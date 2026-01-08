using Discord.WebSocket;
using DiscordBot.Bot.ViewModels.Pages;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DiscordBot.Bot.Pages.Admin;

/// <summary>
/// Page model for the Global Rat Watch Analytics Dashboard.
/// Displays cross-guild analytics metrics, charts, and leaderboards for all guilds.
/// </summary>
[Authorize(Policy = "RequireAdmin")]
public class RatWatchAnalyticsModel : PageModel
{
    private readonly IRatWatchRepository _ratWatchRepository;
    private readonly IRatRecordRepository _ratRecordRepository;
    private readonly IGuildRatWatchSettingsRepository _ratWatchSettingsRepository;
    private readonly IGuildService _guildService;
    private readonly DiscordSocketClient _discordClient;
    private readonly ILogger<RatWatchAnalyticsModel> _logger;

    public RatWatchAnalyticsModel(
        IRatWatchRepository ratWatchRepository,
        IRatRecordRepository ratRecordRepository,
        IGuildRatWatchSettingsRepository ratWatchSettingsRepository,
        IGuildService guildService,
        DiscordSocketClient discordClient,
        ILogger<RatWatchAnalyticsModel> logger)
    {
        _ratWatchRepository = ratWatchRepository;
        _ratRecordRepository = ratRecordRepository;
        _ratWatchSettingsRepository = ratWatchSettingsRepository;
        _guildService = guildService;
        _discordClient = discordClient;
        _logger = logger;
    }

    /// <summary>
    /// The global analytics view model with all chart data and metrics.
    /// </summary>
    public RatWatchAnalyticsViewModel ViewModel { get; private set; } = new();

    /// <summary>
    /// Optional guild ID filter (null = all guilds).
    /// </summary>
    [BindProperty(SupportsGet = true)]
    public ulong? GuildId { get; set; }

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
    /// List of all guilds with Rat Watch enabled for the guild filter dropdown.
    /// </summary>
    public List<GuildSummaryDto> EnabledGuilds { get; private set; } = new();

    /// <summary>
    /// Handles GET requests to display the global analytics dashboard.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The page result.</returns>
    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Loading global Rat Watch analytics (GuildId filter: {GuildId})", GuildId?.ToString() ?? "All");

        // Set defaults if not specified (normalize to midnight UTC)
        StartDate = StartDate.HasValue ? StartDate.Value.Date : DateTime.UtcNow.AddDays(-30).Date;
        EndDate = EndDate.HasValue ? EndDate.Value.Date : DateTime.UtcNow.Date;

        // Query range: start at beginning of StartDate (00:00:00) and end at beginning of day after EndDate (00:00:00)
        // This ensures we include all records from the entire EndDate day (up to 23:59:59.999)
        var start = StartDate.Value;
        var end = EndDate.Value.AddDays(1);

        _logger.LogDebug("Analytics date range: {Start} to {End}", start, end);

        try
        {
            // Load all guilds with Rat Watch enabled for the filter dropdown
            var allSettings = await _ratWatchSettingsRepository.GetAllAsync(cancellationToken);
            var enabledSettings = allSettings.Where(s => s.IsEnabled).ToList();

            // Parallelize guild lookups
            var guildTasks = enabledSettings.Select(s => _guildService.GetGuildByIdAsync(s.GuildId, cancellationToken));
            var guilds = await Task.WhenAll(guildTasks);
            EnabledGuilds = guilds
                .Where(g => g != null)
                .Select(g => new GuildSummaryDto
                {
                    GuildId = g!.Id,
                    Name = g.Name,
                    IconUrl = g.IconUrl
                })
                .ToList();

            _logger.LogDebug("Found {Count} guilds with Rat Watch enabled", EnabledGuilds.Count);

            // Load analytics data from repository in parallel
            var summaryTask = _ratWatchRepository.GetAnalyticsSummaryAsync(GuildId, start, end, cancellationToken);
            var timeSeriesTask = _ratWatchRepository.GetTimeSeriesAsync(GuildId, start, end, cancellationToken);
            var heatmapTask = GuildId.HasValue
                ? _ratWatchRepository.GetActivityHeatmapAsync(GuildId.Value, start, end, cancellationToken)
                : Task.FromResult(Enumerable.Empty<ActivityHeatmapDto>());

            await Task.WhenAll(summaryTask, timeSeriesTask, heatmapTask);
            var summary = summaryTask.Result;
            var timeSeries = timeSeriesTask.Result;
            var heatmap = heatmapTask.Result;

            // Load leaderboards (note: GetUserMetricsAsync doesn't support global, so we'll aggregate manually if needed)
            List<RatWatchUserMetricsDto> mostWatched = new();
            List<RatWatchUserMetricsDto> biggestRats = new();
            List<AccuserMetricsDto> topAccusers = new();

            if (GuildId.HasValue)
            {
                // Single guild - parallelize repository calls
                var mostWatchedTask = _ratRecordRepository.GetUserMetricsAsync(GuildId.Value, "watched", 10, cancellationToken);
                var biggestRatsTask = _ratRecordRepository.GetUserMetricsAsync(GuildId.Value, "guilty", 10, cancellationToken);
                var topAccusersTask = GetTopAccusersAsync(GuildId.Value, 10, cancellationToken);

                await Task.WhenAll(mostWatchedTask, biggestRatsTask, topAccusersTask);

                // Parallelize username resolution
                var mostWatchedNamesTask = ResolveUsernamesAsync(GuildId.Value, mostWatchedTask.Result);
                var biggestRatsNamesTask = ResolveUsernamesAsync(GuildId.Value, biggestRatsTask.Result);
                var topAccusersNamesTask = ResolveAccuserUsernamesAsync(GuildId.Value, topAccusersTask.Result);

                await Task.WhenAll(mostWatchedNamesTask, biggestRatsNamesTask, topAccusersNamesTask);

                mostWatched = mostWatchedNamesTask.Result;
                biggestRats = biggestRatsNamesTask.Result;
                topAccusers = topAccusersNamesTask.Result;
            }
            else
            {
                // Global aggregation across all guilds
                _logger.LogDebug("Aggregating global leaderboards across all enabled guilds");
                // For global view, we'll leave leaderboards empty or aggregate manually
                // This is a simplified implementation - full aggregation would require cross-guild user matching
            }

            // Get guild name if filtering
            string? guildName = null;
            if (GuildId.HasValue)
            {
                var guild = await _guildService.GetGuildByIdAsync(GuildId.Value, cancellationToken);
                guildName = guild?.Name;
            }

            // Build view model
            ViewModel = new RatWatchAnalyticsViewModel
            {
                GuildId = GuildId ?? 0,
                GuildName = guildName ?? "All Guilds",
                GuildIconUrl = null,
                Summary = summary,
                TimeSeries = timeSeries.ToList(),
                Heatmap = heatmap.ToList(),
                MostWatched = mostWatched,
                TopAccusers = topAccusers,
                BiggestRats = biggestRats,
                StartDate = start,
                EndDate = end.AddDays(-1) // Show the actual end date (not +1)
            };

            _logger.LogInformation(
                "Global analytics loaded successfully. Total guilds enabled: {GuildsEnabled}, Total watches: {TotalWatches}, Guilty rate: {GuiltyRate:F2}%",
                EnabledGuilds.Count, summary.TotalWatches, summary.GuiltyRate);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load global analytics data (GuildId filter: {GuildId})", GuildId?.ToString() ?? "All");
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
    /// Resolves usernames for user metrics entries.
    /// </summary>
    private async Task<List<RatWatchUserMetricsDto>> ResolveUsernamesAsync(
        ulong guildId,
        IEnumerable<RatWatchUserMetricsDto> metrics)
    {
        var metricsList = metrics.ToList();
        var usernameTasks = metricsList.Select(m => GetUsernameAsync(m.UserId, guildId));
        var usernames = await Task.WhenAll(usernameTasks);

        return metricsList.Zip(usernames, (m, username) => m with { Username = username }).ToList();
    }

    /// <summary>
    /// Resolves usernames for accuser metrics entries.
    /// </summary>
    private async Task<List<AccuserMetricsDto>> ResolveAccuserUsernamesAsync(
        ulong guildId,
        IEnumerable<AccuserMetricsDto> metrics)
    {
        var metricsList = metrics.ToList();
        var usernameTasks = metricsList.Select(m => GetUsernameAsync(m.UserId, guildId));
        var usernames = await Task.WhenAll(usernameTasks);

        return metricsList.Zip(usernames, (m, username) => m with { Username = username }).ToList();
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

/// <summary>
/// Guild summary for filter dropdown.
/// </summary>
public record GuildSummaryDto
{
    public ulong GuildId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? IconUrl { get; init; }
}
