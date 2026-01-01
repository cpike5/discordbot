using Discord.WebSocket;
using DiscordBot.Bot.ViewModels.Pages;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DiscordBot.Bot.Pages.Guilds.Analytics;

/// <summary>
/// Page model for the Guild Moderation Analytics Dashboard.
/// Displays moderation metrics, trends, case distribution, and repeat offender tracking.
/// </summary>
[Authorize(Policy = "RequireModerator")]
public class ModerationModel : PageModel
{
    private readonly IModerationAnalyticsService _analyticsService;
    private readonly IGuildService _guildService;
    private readonly DiscordSocketClient _discordClient;
    private readonly ILogger<ModerationModel> _logger;

    public ModerationModel(
        IModerationAnalyticsService analyticsService,
        IGuildService guildService,
        DiscordSocketClient discordClient,
        ILogger<ModerationModel> logger)
    {
        _analyticsService = analyticsService;
        _guildService = guildService;
        _discordClient = discordClient;
        _logger = logger;
    }

    /// <summary>
    /// The moderation analytics view model with all chart data and metrics.
    /// </summary>
    public ModerationAnalyticsViewModel ViewModel { get; private set; } = new();

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
    /// Handles GET requests to display the moderation analytics dashboard.
    /// </summary>
    /// <param name="guildId">The guild's Discord snowflake ID from route parameter.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The page result.</returns>
    public async Task<IActionResult> OnGetAsync(ulong guildId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Loading moderation analytics for guild {GuildId}", guildId);

        // Set defaults if not specified (normalize to midnight UTC)
        StartDate = StartDate.HasValue ? StartDate.Value.Date : DateTime.UtcNow.AddDays(-7).Date;
        EndDate = EndDate.HasValue ? EndDate.Value.Date : DateTime.UtcNow.Date;

        // Query range: start at beginning of StartDate (00:00:00) and end at beginning of day after EndDate (00:00:00)
        // This ensures we include all records from the entire EndDate day (up to 23:59:59.999)
        var start = StartDate.Value;
        var end = EndDate.Value.AddDays(1);

        _logger.LogDebug("Moderation analytics date range: {Start} to {End}", start, end);

        try
        {
            // Get guild info
            var guild = await _guildService.GetGuildByIdAsync(guildId, cancellationToken);
            if (guild == null)
            {
                _logger.LogWarning("Guild {GuildId} not found", guildId);
                return NotFound();
            }

            // Load analytics data from service
            var summary = await _analyticsService.GetSummaryAsync(guildId, start, end, cancellationToken);
            var trends = await _analyticsService.GetTrendsAsync(guildId, start, end, cancellationToken);
            var distribution = await _analyticsService.GetCaseDistributionAsync(guildId, start, end, cancellationToken);
            var repeatOffenders = await _analyticsService.GetRepeatOffendersAsync(guildId, start, end, 10, cancellationToken);
            var moderatorWorkload = await _analyticsService.GetModeratorWorkloadAsync(guildId, start, end, 5, cancellationToken);

            // Resolve usernames for repeat offenders
            var repeatOffendersWithNames = await ResolveRepeatOffenderUsernamesAsync(guildId, repeatOffenders);

            // Resolve usernames for moderators
            var moderatorWorkloadWithNames = await ResolveModeratorUsernamesAsync(guildId, moderatorWorkload);

            // Build view model
            ViewModel = new ModerationAnalyticsViewModel
            {
                GuildId = guildId,
                GuildName = guild.Name,
                GuildIconUrl = guild.IconUrl,
                Summary = summary,
                Trends = trends.ToList(),
                Distribution = distribution,
                RepeatOffenders = repeatOffendersWithNames,
                ModeratorWorkload = moderatorWorkloadWithNames,
                StartDate = start,
                EndDate = end.AddDays(-1) // Show the actual end date (not +1)
            };

            _logger.LogInformation(
                "Moderation analytics loaded successfully for guild {GuildId}. Total cases: {TotalCases}, Avg cases/day: {CasesPerDay:F1}",
                guildId, summary.TotalCases, summary.CasesPerDay);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load moderation analytics data for guild {GuildId}", guildId);
            TempData["ErrorMessage"] = "Failed to load analytics data. Please try again.";
            // Return page with empty data
        }

        return Page();
    }

    /// <summary>
    /// Resolves usernames for repeat offender entries.
    /// </summary>
    private async Task<List<RepeatOffenderDto>> ResolveRepeatOffenderUsernamesAsync(
        ulong guildId,
        IEnumerable<RepeatOffenderDto> offenders)
    {
        var result = new List<RepeatOffenderDto>();
        foreach (var offender in offenders)
        {
            var username = await GetUsernameAsync(offender.UserId, guildId);
            var avatarUrl = await GetAvatarUrlAsync(offender.UserId, guildId);
            result.Add(offender with { Username = username, AvatarUrl = avatarUrl });
        }
        return result;
    }

    /// <summary>
    /// Resolves usernames for moderator workload entries.
    /// </summary>
    private async Task<List<ModeratorWorkloadDto>> ResolveModeratorUsernamesAsync(
        ulong guildId,
        IEnumerable<ModeratorWorkloadDto> moderators)
    {
        var result = new List<ModeratorWorkloadDto>();
        foreach (var moderator in moderators)
        {
            var username = await GetUsernameAsync(moderator.ModeratorId, guildId);
            var avatarUrl = await GetAvatarUrlAsync(moderator.ModeratorId, guildId);
            result.Add(moderator with { ModeratorUsername = username, AvatarUrl = avatarUrl });
        }
        return result;
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

    /// <summary>
    /// Gets the avatar URL for a Discord user.
    /// </summary>
    private async Task<string?> GetAvatarUrlAsync(ulong userId, ulong guildId)
    {
        try
        {
            var guild = _discordClient.GetGuild(guildId);
            if (guild == null)
            {
                return null;
            }

            var user = guild.GetUser(userId);
            if (user != null)
            {
                return user.GetAvatarUrl() ?? user.GetDefaultAvatarUrl();
            }

            // Try downloading users if not in cache
            if (!guild.HasAllMembers)
            {
                await guild.DownloadUsersAsync();
                user = guild.GetUser(userId);
                if (user != null)
                {
                    return user.GetAvatarUrl() ?? user.GetDefaultAvatarUrl();
                }
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get avatar URL for user {UserId} in guild {GuildId}", userId, guildId);
            return null;
        }
    }
}
