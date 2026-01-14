using Discord.WebSocket;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DiscordBot.Bot.Pages.Portal;

/// <summary>
/// Base class for Portal pages that require guild membership authorization.
/// Provides common authorization logic while supporting the landing page UX pattern
/// where unauthenticated users see a landing page instead of being redirected.
/// </summary>
public abstract class PortalPageModelBase : PageModel
{
    private readonly IGuildService _guildService;
    private readonly DiscordSocketClient _discordClient;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="PortalPageModelBase"/> class.
    /// </summary>
    protected PortalPageModelBase(
        IGuildService guildService,
        DiscordSocketClient discordClient,
        UserManager<ApplicationUser> userManager,
        ILogger logger)
    {
        _guildService = guildService;
        _discordClient = discordClient;
        _userManager = userManager;
        _logger = logger;
    }

    /// <summary>
    /// Gets the guild's Discord snowflake ID.
    /// </summary>
    public ulong GuildId { get; set; }

    /// <summary>
    /// Gets the guild name.
    /// </summary>
    public string GuildName { get; set; } = string.Empty;

    /// <summary>
    /// Gets the guild icon URL.
    /// </summary>
    public string? GuildIconUrl { get; set; }

    /// <summary>
    /// Gets whether the bot is online (connected to Discord gateway).
    /// </summary>
    public bool IsOnline { get; set; }

    /// <summary>
    /// Gets whether the user is authenticated with Discord OAuth.
    /// When false, display the landing page instead of the full portal interface.
    /// </summary>
    public bool IsAuthenticated { get; set; }

    /// <summary>
    /// Gets whether the authenticated user is authorized to view this portal.
    /// True when user is a member of the guild.
    /// </summary>
    public bool IsAuthorized { get; set; }

    /// <summary>
    /// Gets the login URL with return URL for Discord OAuth.
    /// </summary>
    public string LoginUrl { get; set; } = string.Empty;

    /// <summary>
    /// Result of the portal authorization check.
    /// </summary>
    protected enum PortalAuthResult
    {
        /// <summary>
        /// Guild not found in database or Discord client.
        /// </summary>
        GuildNotFound,

        /// <summary>
        /// User is not authenticated - show landing page.
        /// </summary>
        ShowLandingPage,

        /// <summary>
        /// User is authenticated but not a member of the guild.
        /// </summary>
        NotGuildMember,

        /// <summary>
        /// User is authenticated and is a guild member - show full portal.
        /// </summary>
        Authorized
    }

    /// <summary>
    /// Context containing guild information after authorization check.
    /// </summary>
    protected class PortalAuthContext
    {
        /// <summary>
        /// Gets or sets the guild DTO from the database.
        /// </summary>
        public required GuildDto Guild { get; init; }

        /// <summary>
        /// Gets or sets the Discord socket guild.
        /// </summary>
        public required SocketGuild SocketGuild { get; init; }
    }

    /// <summary>
    /// Performs portal authorization check, setting common properties and returning the result.
    /// This method handles the common pattern of:
    /// 1. Validating the guild exists
    /// 2. Setting base properties (GuildId, GuildName, etc.)
    /// 3. Checking authentication state
    /// 4. Verifying guild membership for authenticated users
    /// </summary>
    /// <param name="guildId">The guild ID from the route.</param>
    /// <param name="portalName">The portal name for logging (e.g., "TTS", "Soundboard").</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A tuple containing the auth result and context (if authorized).</returns>
    protected async Task<(PortalAuthResult Result, PortalAuthContext? Context)> CheckPortalAuthorizationAsync(
        ulong guildId,
        string portalName,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("User {UserId} accessing {PortalName} Portal for guild {GuildId}",
            User.Identity?.Name ?? "anonymous", portalName, guildId);

        // Get guild info - return NotFound if not found (don't reveal guild doesn't exist)
        var guild = await _guildService.GetGuildByIdAsync(guildId, cancellationToken);
        if (guild == null)
        {
            _logger.LogWarning("Guild {GuildId} not found", guildId);
            return (PortalAuthResult.GuildNotFound, null);
        }

        // Check if Discord guild is available
        var socketGuild = _discordClient.GetGuild(guildId);
        if (socketGuild == null)
        {
            _logger.LogWarning("Guild {GuildId} not found in Discord client", guildId);
            return (PortalAuthResult.GuildNotFound, null);
        }

        // Set basic guild info for landing page (needed for both auth states)
        GuildId = guildId;
        GuildName = guild.Name;
        GuildIconUrl = guild.IconUrl;
        IsOnline = _discordClient.ConnectionState == Discord.ConnectionState.Connected;

        // Build login URL with return URL
        var returnUrl = HttpContext.Request.Path.ToString();
        LoginUrl = $"/Account/Login?returnUrl={Uri.EscapeDataString(returnUrl)}";

        // Check authentication state
        IsAuthenticated = User.Identity?.IsAuthenticated ?? false;

        if (!IsAuthenticated)
        {
            _logger.LogDebug("Unauthenticated user viewing landing page for guild {GuildId}", guildId);
            return (PortalAuthResult.ShowLandingPage, new PortalAuthContext { Guild = guild, SocketGuild = socketGuild });
        }

        // User is authenticated - check guild membership
        var user = await _userManager.GetUserAsync(User);
        if (user == null || !user.DiscordUserId.HasValue)
        {
            _logger.LogDebug("User not found or no Discord linked, showing landing page for guild {GuildId}", guildId);
            IsAuthenticated = false; // Treat as unauthenticated for UI purposes
            return (PortalAuthResult.ShowLandingPage, new PortalAuthContext { Guild = guild, SocketGuild = socketGuild });
        }

        // Check if user is a member of the guild
        var guildUser = socketGuild.GetUser(user.DiscordUserId.Value);
        if (guildUser == null)
        {
            _logger.LogDebug("User {DiscordUserId} is not a member of guild {GuildId}",
                user.DiscordUserId.Value, guildId);
            return (PortalAuthResult.NotGuildMember, new PortalAuthContext { Guild = guild, SocketGuild = socketGuild });
        }

        // User is authenticated and authorized
        IsAuthorized = true;
        _logger.LogDebug("User {DiscordUserId} authorized for {PortalName} Portal in guild {GuildId}",
            user.DiscordUserId.Value, portalName, guildId);

        return (PortalAuthResult.Authorized, new PortalAuthContext { Guild = guild, SocketGuild = socketGuild });
    }

    /// <summary>
    /// Converts a <see cref="PortalAuthResult"/> to the appropriate <see cref="IActionResult"/>.
    /// </summary>
    /// <param name="result">The auth result.</param>
    /// <returns>The action result, or null if the page should continue loading.</returns>
    protected IActionResult? GetAuthResultAction(PortalAuthResult result)
    {
        return result switch
        {
            PortalAuthResult.GuildNotFound => NotFound(),
            PortalAuthResult.NotGuildMember => Forbid(),
            PortalAuthResult.ShowLandingPage => null, // Continue to Page()
            PortalAuthResult.Authorized => null, // Continue to load full portal
            _ => NotFound()
        };
    }

    /// <summary>
    /// Builds a list of voice channels for the guild.
    /// </summary>
    /// <param name="socketGuild">The Discord socket guild.</param>
    /// <returns>List of voice channel information.</returns>
    protected static List<VoiceChannelInfo> BuildVoiceChannelList(SocketGuild socketGuild)
    {
        var voiceChannels = new List<VoiceChannelInfo>();
        foreach (var channel in socketGuild.VoiceChannels.Where(c => c != null).OrderBy(c => c.Position))
        {
            voiceChannels.Add(new VoiceChannelInfo
            {
                Id = channel.Id,
                Name = channel.Name,
                MemberCount = channel.ConnectedUsers.Count
            });
        }
        return voiceChannels;
    }
}

/// <summary>
/// DTO for voice channel information in Portal pages.
/// </summary>
public class VoiceChannelInfo
{
    /// <summary>
    /// Gets or sets the Discord snowflake ID of the voice channel.
    /// </summary>
    public ulong Id { get; set; }

    /// <summary>
    /// Gets or sets the name of the voice channel.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the number of members currently in the channel.
    /// </summary>
    public int MemberCount { get; set; }
}
