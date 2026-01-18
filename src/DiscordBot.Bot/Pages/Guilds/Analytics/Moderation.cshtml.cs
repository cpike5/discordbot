using Discord.WebSocket;
using DiscordBot.Bot.Configuration;
using DiscordBot.Bot.ViewModels.Components;
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
[Authorize(Policy = "GuildAccess")]
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

            // Load analytics data from service in parallel
            var summaryTask = _analyticsService.GetSummaryAsync(guildId, start, end, cancellationToken);
            var trendsTask = _analyticsService.GetTrendsAsync(guildId, start, end, cancellationToken);
            var distributionTask = _analyticsService.GetCaseDistributionAsync(guildId, start, end, cancellationToken);
            var repeatOffendersTask = _analyticsService.GetRepeatOffendersAsync(guildId, start, end, 10, cancellationToken);
            var moderatorWorkloadTask = _analyticsService.GetModeratorWorkloadAsync(guildId, start, end, 5, cancellationToken);

            await Task.WhenAll(summaryTask, trendsTask, distributionTask, repeatOffendersTask, moderatorWorkloadTask);

            var summary = await summaryTask;
            var trends = await trendsTask;
            var distribution = await distributionTask;
            var repeatOffenders = await repeatOffendersTask;
            var moderatorWorkload = await moderatorWorkloadTask;

            // Resolve usernames in parallel
            var repeatOffendersWithNamesTask = ResolveRepeatOffenderUsernamesAsync(guildId, repeatOffenders);
            var moderatorWorkloadWithNamesTask = ResolveModeratorUsernamesAsync(guildId, moderatorWorkload);

            await Task.WhenAll(repeatOffendersWithNamesTask, moderatorWorkloadWithNamesTask);

            var repeatOffendersWithNames = await repeatOffendersWithNamesTask;
            var moderatorWorkloadWithNames = await moderatorWorkloadWithNamesTask;

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

            // Populate guild layout ViewModels
            Breadcrumb = new GuildBreadcrumbViewModel
            {
                Items = new List<BreadcrumbItem>
                {
                    new() { Label = "Home", Url = "/" },
                    new() { Label = "Servers", Url = "/Guilds" },
                    new() { Label = guild.Name, Url = $"/Guilds/Details/{guildId}" },
                    new() { Label = "Analytics", Url = $"/Guilds/Analytics/{guildId}" },
                    new() { Label = "Moderation", IsCurrent = true }
                }
            };

            Header = new GuildHeaderViewModel
            {
                GuildId = guild.Id,
                GuildName = guild.Name,
                GuildIconUrl = guild.IconUrl,
                PageTitle = "Moderation Analytics",
                PageDescription = $"Moderation metrics and trends for {guild.Name}"
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
            _logger.LogError(ex, "Failed to load moderation analytics data for guild {GuildId}", guildId);
            TempData["ErrorMessage"] = "Failed to load analytics data. Please try again.";
            // Return page with empty data
        }

        return Page();
    }

    /// <summary>
    /// Resolves usernames for repeat offender entries in parallel.
    /// </summary>
    private async Task<List<RepeatOffenderDto>> ResolveRepeatOffenderUsernamesAsync(
        ulong guildId,
        IEnumerable<RepeatOffenderDto> offenders)
    {
        var offenderList = offenders.ToList();
        if (offenderList.Count == 0)
        {
            return [];
        }

        // Pre-download guild members once to avoid multiple concurrent downloads
        await EnsureGuildMembersDownloadedAsync(guildId);

        var tasks = offenderList.Select(async offender =>
        {
            var username = GetUsernameFromCache(offender.UserId, guildId);
            var avatarUrl = GetAvatarUrlFromCache(offender.UserId, guildId);
            return offender with { Username = username, AvatarUrl = avatarUrl };
        });

        var results = await Task.WhenAll(tasks);
        return results.ToList();
    }

    /// <summary>
    /// Resolves usernames for moderator workload entries in parallel.
    /// </summary>
    private async Task<List<ModeratorWorkloadDto>> ResolveModeratorUsernamesAsync(
        ulong guildId,
        IEnumerable<ModeratorWorkloadDto> moderators)
    {
        var moderatorList = moderators.ToList();
        if (moderatorList.Count == 0)
        {
            return [];
        }

        // Pre-download guild members once to avoid multiple concurrent downloads
        await EnsureGuildMembersDownloadedAsync(guildId);

        var tasks = moderatorList.Select(async moderator =>
        {
            var username = GetUsernameFromCache(moderator.ModeratorId, guildId);
            var avatarUrl = GetAvatarUrlFromCache(moderator.ModeratorId, guildId);
            return moderator with { ModeratorUsername = username, AvatarUrl = avatarUrl };
        });

        var results = await Task.WhenAll(tasks);
        return results.ToList();
    }

    /// <summary>
    /// Ensures guild members are downloaded before parallel lookups.
    /// </summary>
    private async Task EnsureGuildMembersDownloadedAsync(ulong guildId)
    {
        try
        {
            var guild = _discordClient.GetGuild(guildId);
            if (guild != null && !guild.HasAllMembers)
            {
                await guild.DownloadUsersAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to download guild members for guild {GuildId}", guildId);
        }
    }

    /// <summary>
    /// Gets the display name for a Discord user from cache (synchronous).
    /// </summary>
    private string GetUsernameFromCache(ulong userId, ulong guildId)
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
    /// Gets the avatar URL for a Discord user from cache (synchronous).
    /// </summary>
    private string? GetAvatarUrlFromCache(ulong userId, ulong guildId)
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

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get avatar URL for user {UserId} in guild {GuildId}", userId, guildId);
            return null;
        }
    }
}
