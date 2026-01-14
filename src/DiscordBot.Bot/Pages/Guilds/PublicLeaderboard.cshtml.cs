using Discord.WebSocket;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DiscordBot.Bot.Pages.Guilds;

/// <summary>
/// Public leaderboard page for a guild's Rat Watch feature.
/// Requires authentication and guild membership verification.
/// </summary>
[AllowAnonymous]
public class PublicLeaderboardModel : PageModel
{
    private readonly IRatRecordRepository _ratRecordRepository;
    private readonly IRatWatchRepository _ratWatchRepository;
    private readonly IGuildRatWatchSettingsRepository _ratWatchSettingsRepository;
    private readonly IGuildService _guildService;
    private readonly DiscordSocketClient _discordClient;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IConfiguration _configuration;
    private readonly ILogger<PublicLeaderboardModel> _logger;

    public PublicLeaderboardModel(
        IRatRecordRepository ratRecordRepository,
        IRatWatchRepository ratWatchRepository,
        IGuildRatWatchSettingsRepository ratWatchSettingsRepository,
        IGuildService guildService,
        DiscordSocketClient discordClient,
        UserManager<ApplicationUser> userManager,
        IConfiguration configuration,
        ILogger<PublicLeaderboardModel> logger)
    {
        _ratRecordRepository = ratRecordRepository;
        _ratWatchRepository = ratWatchRepository;
        _ratWatchSettingsRepository = ratWatchSettingsRepository;
        _guildService = guildService;
        _discordClient = discordClient;
        _userManager = userManager;
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>
    /// The guild's Discord snowflake ID.
    /// </summary>
    public ulong GuildId { get; private set; }

    /// <summary>
    /// The guild's name.
    /// </summary>
    public string GuildName { get; private set; } = string.Empty;

    /// <summary>
    /// Optional guild icon URL.
    /// </summary>
    public string? GuildIconUrl { get; private set; }

    /// <summary>
    /// Whether the public leaderboard is enabled for this guild.
    /// </summary>
    public bool IsLeaderboardPublic { get; private set; }

    /// <summary>
    /// Fun stats highlights (longest streaks, biggest landslides, etc.).
    /// </summary>
    public RatWatchFunStatsDto? FunStats { get; private set; }

    /// <summary>
    /// Leaderboard of users with the most guilty verdicts (top 25).
    /// </summary>
    public List<PublicLeaderboardEntryDto> Leaderboard { get; private set; } = new();

    /// <summary>
    /// Recent guilty verdicts (last 10).
    /// </summary>
    public List<RecentIncidentDto> RecentIncidents { get; private set; } = new();

    /// <summary>
    /// Application title from configuration.
    /// </summary>
    public string AppTitle { get; private set; } = "Discord Bot";

    /// <summary>
    /// Gets whether the user is authenticated with Discord OAuth.
    /// When false, displays the landing page instead of leaderboard data.
    /// </summary>
    public bool IsAuthenticated { get; private set; }

    /// <summary>
    /// Gets whether the authenticated user is authorized to view this leaderboard.
    /// True when user is a member of the guild.
    /// </summary>
    public bool IsAuthorized { get; private set; }

    /// <summary>
    /// Gets the login URL with return URL for Discord OAuth.
    /// </summary>
    public string LoginUrl { get; private set; } = string.Empty;

    /// <summary>
    /// Handles GET requests to display the public leaderboard.
    /// </summary>
    /// <param name="guildId">The guild's Discord snowflake ID from route parameter.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The page result or NotFound if leaderboard is not public.</returns>
    public async Task<IActionResult> OnGetAsync(ulong guildId, CancellationToken cancellationToken = default)
    {
        GuildId = guildId;

        _logger.LogInformation("Public leaderboard requested for guild {GuildId}", guildId);

        // Build login URL with return URL
        var returnUrl = HttpContext.Request.Path.ToString();
        LoginUrl = $"/Account/Login?returnUrl={Uri.EscapeDataString(returnUrl)}";

        // Get application title early (needed for landing page)
        AppTitle = _configuration.GetValue<string>("Application:Title") ?? "Discord Bot";

        try
        {
            // Phase 1: Parallelize validation calls
            var guildTask = _guildService.GetGuildByIdAsync(guildId, cancellationToken);
            var settingsTask = _ratWatchSettingsRepository.GetByGuildIdAsync(guildId, cancellationToken);

            await Task.WhenAll(guildTask, settingsTask);

            var guild = await guildTask;
            var settings = await settingsTask;

            if (guild == null)
            {
                _logger.LogWarning("Guild {GuildId} not found", guildId);
                return NotFound();
            }

            GuildName = guild.Name;
            GuildIconUrl = guild.IconUrl;

            if (settings == null || !settings.IsEnabled)
            {
                _logger.LogWarning("Rat Watch not enabled for guild {GuildId}", guildId);
                return NotFound("Rat Watch is not enabled for this server");
            }

            IsLeaderboardPublic = settings.PublicLeaderboardEnabled;
            if (!IsLeaderboardPublic)
            {
                _logger.LogInformation("Public leaderboard not enabled for guild {GuildId}", guildId);
                // Still show page, but with a message that it's not public
                return Page();
            }

            // Check authentication state
            IsAuthenticated = User.Identity?.IsAuthenticated ?? false;

            if (!IsAuthenticated)
            {
                _logger.LogDebug("Unauthenticated user viewing landing page for guild {GuildId}", guildId);
                return Page();
            }

            // User is authenticated - check guild membership
            var applicationUser = await _userManager.GetUserAsync(User);
            if (applicationUser == null || !applicationUser.DiscordUserId.HasValue)
            {
                _logger.LogDebug("User not found or no Discord linked, showing landing page for guild {GuildId}", guildId);
                IsAuthenticated = false; // Treat as unauthenticated for UI purposes
                return Page();
            }

            // Check if user is a member of the guild
            var socketGuild = _discordClient.GetGuild(guildId);
            if (socketGuild == null)
            {
                _logger.LogWarning("Guild {GuildId} not found in Discord client", guildId);
                return NotFound();
            }

            var guildUser = socketGuild.GetUser(applicationUser.DiscordUserId.Value);
            if (guildUser == null)
            {
                _logger.LogDebug("User {DiscordUserId} is not a member of guild {GuildId}",
                    applicationUser.DiscordUserId.Value, guildId);
                return Forbid();
            }

            // User is authenticated and authorized
            IsAuthorized = true;
            _logger.LogDebug("User {DiscordUserId} authorized to view leaderboard for guild {GuildId}",
                applicationUser.DiscordUserId.Value, guildId);

            // Phase 2: Parallelize data loading calls
            var funStatsTask = _ratRecordRepository.GetFunStatsAsync(guildId, cancellationToken);
            var userMetricsTask = _ratRecordRepository.GetUserMetricsAsync(guildId, "guilty", 25, cancellationToken);
            var watchesTask = _ratWatchRepository.GetAllAsync(cancellationToken);

            await Task.WhenAll(funStatsTask, userMetricsTask, watchesTask);

            FunStats = await funStatsTask;
            var userMetrics = await userMetricsTask;
            var allWatches = await watchesTask;

            // Phase 3: Parallelize username resolution for leaderboard
            var leaderboardTasks = userMetrics.Select(async (metric, index) =>
            {
                var username = await GetUsernameAsync(metric.UserId, guildId);
                return new PublicLeaderboardEntryDto
                {
                    Rank = index + 1,
                    Username = username,
                    RatCount = metric.GuiltyCount,
                    LastIncidentDate = metric.LastIncidentDate
                };
            });
            Leaderboard = (await Task.WhenAll(leaderboardTasks)).ToList();

            // Filter and get recent guilty verdicts (last 10)
            var recentGuiltyWatches = allWatches
                .Where(w => w.GuildId == guildId && w.Status == Core.Enums.RatWatchStatus.Guilty)
                .OrderByDescending(w => w.VotingEndedAt ?? w.CreatedAt)
                .Take(10)
                .ToList();

            // Phase 3: Parallelize username resolution for recent incidents
            var incidentTasks = recentGuiltyWatches.Select(async watch =>
            {
                var username = await GetUsernameAsync(watch.AccusedUserId, guildId);
                var guiltyVotes = watch.Votes?.Count(v => v.IsGuiltyVote) ?? 0;
                var notGuiltyVotes = watch.Votes?.Count(v => !v.IsGuiltyVote) ?? 0;

                return new RecentIncidentDto
                {
                    Date = watch.VotingEndedAt ?? watch.CreatedAt,
                    Username = username,
                    Outcome = "Guilty",
                    VoteTally = $"{guiltyVotes}-{notGuiltyVotes}"
                };
            });
            RecentIncidents = (await Task.WhenAll(incidentTasks)).ToList();

            _logger.LogInformation(
                "Public leaderboard loaded for guild {GuildId}. Entries: {EntryCount}, Recent: {RecentCount}",
                guildId, Leaderboard.Count, RecentIncidents.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load public leaderboard for guild {GuildId}", guildId);
            TempData["ErrorMessage"] = "Failed to load leaderboard. Please try again.";
        }

        return Page();
    }

    /// <summary>
    /// Gets the display name for a Discord user in a guild.
    /// Returns username only (not nickname) for privacy.
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
                // Return username only (not DisplayName which includes nickname)
                return user.Username;
            }

            // Try downloading users if not in cache
            if (!guild.HasAllMembers)
            {
                await guild.DownloadUsersAsync();
                user = guild.GetUser(userId);
                if (user != null)
                {
                    return user.Username;
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
/// Public leaderboard entry DTO (privacy-focused).
/// </summary>
public record PublicLeaderboardEntryDto
{
    public int Rank { get; init; }
    public string Username { get; init; } = string.Empty;
    public int RatCount { get; init; }
    public DateTime? LastIncidentDate { get; init; }
}

/// <summary>
/// Recent incident DTO for public display.
/// </summary>
public record RecentIncidentDto
{
    public DateTime Date { get; init; }
    public string Username { get; init; } = string.Empty;
    public string Outcome { get; init; } = string.Empty;
    public string VoteTally { get; init; } = string.Empty;
}
