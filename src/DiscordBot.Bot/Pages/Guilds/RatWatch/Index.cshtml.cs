using DiscordBot.Bot.Extensions;
using DiscordBot.Bot.ViewModels.Components;
using DiscordBot.Bot.ViewModels.Pages;
using DiscordBot.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DiscordBot.Bot.Pages.Guilds.RatWatch;

/// <summary>
/// Page model for the Rat Watch management page.
/// Displays watches, settings, and leaderboard for a guild.
/// </summary>
[Authorize(Policy = "RequireAdmin")]
[Authorize(Policy = "GuildAccess")]
public class IndexModel : GuildPageModelBase
{
    private readonly IRatWatchService _ratWatchService;
    private readonly IGuildService _guildService;
    private readonly IRatWatchRepository _ratWatchRepository;
    private readonly ILogger<IndexModel> _logger;

    public IndexModel(
        IRatWatchService ratWatchService,
        IGuildService guildService,
        IRatWatchRepository ratWatchRepository,
        ILogger<IndexModel> logger)
    {
        _ratWatchService = ratWatchService;
        _guildService = guildService;
        _ratWatchRepository = ratWatchRepository;
        _logger = logger;
    }

    /// <summary>
    /// View model for display properties.
    /// </summary>
    public RatWatchIndexViewModel ViewModel { get; set; } = new();

    /// <summary>
    /// Handles GET requests to display the Rat Watch management page.
    /// </summary>
    /// <param name="guildId">The guild's Discord snowflake ID from route parameter.</param>
    /// <param name="pageNumber">The page number from query parameter (default: 1). Not "page": Razor Pages reserves that route key for the page name.</param>
    /// <param name="pageSize">The page size from query parameter (default: 20).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The page result.</returns>
    public async Task<IActionResult> OnGetAsync(
        ulong guildId,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("User accessing Rat Watch management for guild {GuildId}, page {Page}",
            guildId, pageNumber);

        // Validate pagination parameters
        if (pageNumber < 1) pageNumber = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 20;

        // Get guild info from service
        var guild = await _guildService.GetGuildByIdAsync(guildId, cancellationToken);
        if (guild == null)
        {
            _logger.LogWarning("Guild {GuildId} not found", guildId);
            return NotFound();
        }

        // Get Rat Watch settings first
        var settings = await _ratWatchService.GetGuildSettingsAsync(guildId, cancellationToken);

        // Populate guild layout ViewModels
        var statusDescription = settings.IsEnabled ? "Enabled" : "Disabled";
        PopulateGuildLayout(guild.Id, guild.Name, guild.IconUrl, "ratwatch",
            "Rat Watch", $"Manage accountability trackers and view the hall of shame ({statusDescription})");

        // Get paginated watches
        var (watches, totalCount) = await _ratWatchService.GetByGuildAsync(
            guildId,
            pageNumber,
            pageSize,
            cancellationToken);

        // Get leaderboard
        var leaderboard = await _ratWatchService.GetLeaderboardAsync(guildId, 10, cancellationToken);

        // Get analytics summary (all-time stats for the guild)
        var analyticsSummary = await _ratWatchRepository.GetAnalyticsSummaryAsync(
            guildId,
            null,
            null,
            cancellationToken);

        _logger.LogDebug("Retrieved {Count} watches for guild {GuildId} (page {Page} of {TotalPages})",
            watches.Count(), guildId, pageNumber, (int)Math.Ceiling((double)totalCount / pageSize));

        // Build view model
        ViewModel = RatWatchIndexViewModel.Create(
            guildId,
            guild.Name,
            guild.IconUrl,
            settings,
            watches,
            totalCount,
            leaderboard,
            pageNumber,
            pageSize,
            analyticsSummary);

        return Page();
    }

    /// <summary>
    /// True when the watch exists and belongs to this guild. The route's guildId is what the GuildAccess
    /// policy authorizes, so a watch id from another guild must not be acted on through it.
    /// </summary>
    private async Task<bool> WatchBelongsToGuildAsync(Guid watchId, ulong guildId, CancellationToken cancellationToken)
    {
        var watch = await _ratWatchService.GetByIdAsync(watchId, cancellationToken);
        if (watch == null || watch.GuildId != guildId)
        {
            _logger.LogWarning("Rat Watch {WatchId} is not in guild {GuildId}; refusing the action", watchId, guildId);
            return false;
        }

        return true;
    }

    private static bool IsKnownTimeZone(string timezone)
    {
        try
        {
            return TimeZoneInfo.TryFindSystemTimeZoneById(timezone, out _);
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    /// <summary>
    /// Handles POST requests to cancel a Rat Watch.
    /// </summary>
    /// <param name="guildId">The guild's Discord snowflake ID from route parameter.</param>
    /// <param name="watchId">The watch ID to cancel.</param>
    /// <param name="pageNumber">The current page number to return to.</param>
    /// <param name="pageSize">The current page size.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Redirect to the index page.</returns>
    public async Task<IActionResult> OnPostCancelAsync(
        ulong guildId,
        Guid watchId,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("User attempting to cancel Rat Watch {WatchId} for guild {GuildId}",
            watchId, guildId);

        if (!await WatchBelongsToGuildAsync(watchId, guildId, cancellationToken))
        {
            return NotFound();
        }

        var success = await _ratWatchService.CancelWatchAsync(
            watchId,
            "Cancelled by administrator from Admin UI",
            cancellationToken);

        if (success)
        {
            _logger.LogInformation("Successfully cancelled Rat Watch {WatchId}", watchId);
            TempData.SetSuccessToast("Rat Watch cancelled successfully.");
        }
        else
        {
            _logger.LogWarning("Failed to cancel Rat Watch {WatchId} - not found or already completed", watchId);
            TempData.SetErrorToast("Could not cancel the Rat Watch. It may have already completed or been cancelled.");
        }

        return RedirectToPage("Index", new { guildId, pageNumber, pageSize });
    }

    /// <summary>
    /// Handles POST requests to end voting early on a Rat Watch.
    /// </summary>
    /// <param name="guildId">The guild's Discord snowflake ID from route parameter.</param>
    /// <param name="watchId">The watch ID to end voting on.</param>
    /// <param name="pageNumber">The current page number to return to.</param>
    /// <param name="pageSize">The current page size.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Redirect to the index page.</returns>
    public async Task<IActionResult> OnPostEndVoteAsync(
        ulong guildId,
        Guid watchId,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("User attempting to end vote on Rat Watch {WatchId} for guild {GuildId}",
            watchId, guildId);

        if (!await WatchBelongsToGuildAsync(watchId, guildId, cancellationToken))
        {
            return NotFound();
        }

        var success = await _ratWatchService.FinalizeVotingAsync(watchId, cancellationToken);

        if (success)
        {
            _logger.LogInformation("Successfully ended voting on Rat Watch {WatchId}", watchId);
            TempData.SetSuccessToast("Voting ended and verdict determined.");
        }
        else
        {
            _logger.LogWarning("Failed to end voting on Rat Watch {WatchId} - not found or not in voting status", watchId);
            TempData.SetErrorToast("Could not end voting. The watch may not be in voting status.");
        }

        return RedirectToPage("Index", new { guildId, pageNumber, pageSize });
    }

    /// <summary>
    /// Handles POST requests to update Rat Watch settings.
    /// </summary>
    /// <param name="guildId">The guild's Discord snowflake ID from route parameter.</param>
    /// <param name="timezone">The new timezone setting.</param>
    /// <param name="maxAdvanceHours">The new max advance hours setting.</param>
    /// <param name="votingDurationMinutes">The new voting duration setting.</param>
    /// <param name="isEnabled">Whether Rat Watch is enabled.</param>
    /// <param name="publicLeaderboardEnabled">Whether the public leaderboard is enabled.</param>
    /// <param name="pageNumber">The current page number to return to.</param>
    /// <param name="pageSize">The current page size.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Redirect to the index page.</returns>
    public async Task<IActionResult> OnPostUpdateSettingsAsync(
        ulong guildId,
        [FromForm] string timezone,
        [FromForm] int maxAdvanceHours,
        [FromForm] int votingDurationMinutes,
        [FromForm] bool isEnabled,
        [FromForm] bool publicLeaderboardEnabled,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "User updating Rat Watch settings for guild {GuildId}: timezone={Timezone}, maxHours={MaxHours}, votingMinutes={VotingMinutes}, enabled={Enabled}, publicLeaderboard={PublicLeaderboard}",
            guildId, timezone, maxAdvanceHours, votingDurationMinutes, isEnabled, publicLeaderboardEnabled);

        // Validate parameters
        if (string.IsNullOrWhiteSpace(timezone))
        {
            TempData.SetErrorToast("Timezone is required.");
            return RedirectToPage("Index", new { guildId, pageNumber, pageSize });
        }

        // A name the server cannot resolve would break every time conversion for the guild. The value
        // already stored may be re-submitted as it is (an older unknown name must not block other edits).
        if (!IsKnownTimeZone(timezone))
        {
            var current = await _ratWatchService.GetGuildSettingsAsync(guildId, cancellationToken);
            if (!string.Equals(current.Timezone, timezone, StringComparison.Ordinal))
            {
                TempData.SetErrorToast("Unknown timezone. Use a timezone ID such as \"America/New_York\" or \"UTC\".");
                return RedirectToPage("Index", new { guildId, pageNumber, pageSize });
            }
        }

        if (maxAdvanceHours < 1 || maxAdvanceHours > 168) // 1 week max
        {
            TempData.SetErrorToast("Max advance hours must be between 1 and 168 (1 week).");
            return RedirectToPage("Index", new { guildId, pageNumber, pageSize });
        }

        if (votingDurationMinutes < 1 || votingDurationMinutes > 60)
        {
            TempData.SetErrorToast("Voting duration must be between 1 and 60 minutes.");
            return RedirectToPage("Index", new { guildId, pageNumber, pageSize });
        }

        try
        {
            await _ratWatchService.UpdateGuildSettingsAsync(guildId, settings =>
            {
                settings.Timezone = timezone;
                settings.MaxAdvanceHours = maxAdvanceHours;
                settings.VotingDurationMinutes = votingDurationMinutes;
                settings.IsEnabled = isEnabled;
                settings.PublicLeaderboardEnabled = publicLeaderboardEnabled;
            }, cancellationToken);

            _logger.LogInformation("Successfully updated Rat Watch settings for guild {GuildId}", guildId);
            TempData.SetSuccessToast("Rat Watch settings updated successfully.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update Rat Watch settings for guild {GuildId}", guildId);
            TempData.SetErrorToast("Failed to update settings. Please try again.");
        }

        return RedirectToPage("Index", new { guildId, pageNumber, pageSize });
    }
}
