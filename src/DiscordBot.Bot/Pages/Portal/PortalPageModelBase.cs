using DiscordBot.Bot.Extensions;
using DiscordBot.Bot.Interfaces;
using DiscordBot.Bot.ViewModels.Components;
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
    private readonly IPortalGuildDirectory _guildDirectory;
    private readonly IGuildAudioSettingsRepository _audioSettingsRepository;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="PortalPageModelBase"/> class.
    /// </summary>
    protected PortalPageModelBase(
        IGuildService guildService,
        IPortalGuildDirectory guildDirectory,
        IGuildAudioSettingsRepository audioSettingsRepository,
        UserManager<ApplicationUser> userManager,
        ILogger logger)
    {
        _guildService = guildService;
        _guildDirectory = guildDirectory;
        _audioSettingsRepository = audioSettingsRepository;
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
    /// Gets whether the authenticated user is authenticated with Discord OAuth.
    /// When false, display the landing page instead of the full portal interface.
    /// </summary>
    public bool IsAuthenticated { get; set; }

    /// <summary>
    /// Gets whether the authenticated user is authorized to view this portal.
    /// True when user is a member of the guild.
    /// </summary>
    public bool IsAuthorized { get; set; }

    /// <summary>
    /// Gets whether the guild has switched its member portal off. The page renders the
    /// "portal disabled" notice instead of its content.
    /// </summary>
    public bool IsPortalDisabled { get; set; }

    /// <summary>
    /// Gets whether audio is switched off for this guild (<c>GuildAudioSettings.AudioEnabled</c>).
    /// The portal still opens, with a notice, because the portal switch is independent of it.
    /// </summary>
    public bool IsAudioDisabledForGuild { get; set; }

    /// <summary>
    /// Gets whether the signed-in user may use the dashboard SignalR hub (any Identity role from Viewer up).
    /// Portal members hold no role, so the hub refuses them; pages leave its scripts out for them and the
    /// voice panel reads the portal status endpoint instead.
    /// </summary>
    public bool CanUseDashboardHub =>
        User.IsInRole(IdentitySeeder.Roles.SuperAdmin) ||
        User.IsInRole(IdentitySeeder.Roles.Admin) ||
        User.IsInRole(IdentitySeeder.Roles.Moderator) ||
        User.IsInRole(IdentitySeeder.Roles.Viewer);

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
        /// The guild has switched its member portal off.
        /// </summary>
        PortalDisabled,

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
    }

    /// <summary>
    /// Performs portal authorization check, setting common properties and returning the result.
    /// This method handles the common pattern of:
    /// 1. Validating the guild exists
    /// 2. Setting base properties (GuildId, GuildName, etc.)
    /// 3. Checking that the guild's member portal is switched on
    /// 4. Checking authentication state
    /// 5. Verifying guild membership for authenticated users
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

        // Check if the bot can see the Discord guild
        if (!await _guildDirectory.IsGuildAvailableAsync(guildId, cancellationToken))
        {
            _logger.LogWarning("Guild {GuildId} not found in Discord client", guildId);
            return (PortalAuthResult.GuildNotFound, null);
        }

        // Set basic guild info for the landing and disabled pages (needed for every state)
        GuildId = guildId;
        GuildName = guild.Name;
        GuildIconUrl = guild.IconUrl;
        IsOnline = _guildDirectory.IsBotOnline;

        var context = new PortalAuthContext { Guild = guild };

        // The member portal has its own switch, independent of AudioEnabled (issue #947). It is checked
        // before anything else, so a portal that is off is off for everyone, admins included.
        var audioSettings = await _audioSettingsRepository.GetByGuildIdAsync(guildId, cancellationToken);
        IsAudioDisabledForGuild = audioSettings == null || !audioSettings.AudioEnabled;
        if (audioSettings == null || !audioSettings.EnableMemberPortal)
        {
            _logger.LogDebug("Member portal is not enabled for guild {GuildId}", guildId);
            IsPortalDisabled = true;
            return (PortalAuthResult.PortalDisabled, context);
        }

        // Build login URL with return URL
        var returnUrl = HttpContext.Request.Path.ToString();
        LoginUrl = $"/Account/Login?returnUrl={Uri.EscapeDataString(returnUrl)}";

        // Check authentication state
        IsAuthenticated = User.Identity?.IsAuthenticated ?? false;

        if (!IsAuthenticated)
        {
            _logger.LogDebug("Unauthenticated user viewing landing page for guild {GuildId}", guildId);
            return (PortalAuthResult.ShowLandingPage, context);
        }

        // SuperAdmins and Admins bypass guild membership checks, and they do it before the Discord
        // link is looked at, exactly as PortalGuildMemberAuthorizationHandler does: an admin without
        // a linked Discord account reaches the page the same way the API lets them reach its endpoints
        if (User.IsInRole(IdentitySeeder.Roles.SuperAdmin) || User.IsInRole(IdentitySeeder.Roles.Admin))
        {
            _logger.LogDebug("Admin user {UserName} granted portal access for guild {GuildId}",
                User.Identity?.Name, guildId);
            IsAuthorized = true;
            return (PortalAuthResult.Authorized, context);
        }

        // User is authenticated - check guild membership
        var user = await _userManager.GetUserAsync(User);
        if (user == null || !user.DiscordUserId.HasValue)
        {
            _logger.LogDebug("User not found or no Discord linked, showing landing page for guild {GuildId}", guildId);
            IsAuthenticated = false; // Treat as unauthenticated for UI purposes
            return (PortalAuthResult.ShowLandingPage, context);
        }

        // Check if user is a member of the guild (cache first, then REST API fallback)
        if (!await _guildDirectory.IsMemberAsync(guildId, user.DiscordUserId.Value, cancellationToken))
        {
            _logger.LogDebug("User {DiscordUserId} is not a member of guild {GuildId}",
                user.DiscordUserId.Value, guildId);
            return (PortalAuthResult.NotGuildMember, context);
        }

        // User is authenticated and authorized
        IsAuthorized = true;
        _logger.LogDebug("User {DiscordUserId} authorized for {PortalName} Portal in guild {GuildId}",
            user.DiscordUserId.Value, portalName, guildId);

        return (PortalAuthResult.Authorized, context);
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
            PortalAuthResult.PortalDisabled => Page(), // The page renders the "portal disabled" notice
            PortalAuthResult.ShowLandingPage => null, // Continue to Page()
            PortalAuthResult.Authorized => null, // Continue to load full portal
            _ => NotFound()
        };
    }

    /// <summary>
    /// Builds the voice channel panel for a portal page from the bot's real voice state.
    /// The panel talks to the portal's own endpoints, never to the Viewer-gated admin ones.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="audioService">The audio service, the source of the connection state.</param>
    /// <param name="nowPlayingName">What is playing, or null for nothing.</param>
    protected VoiceChannelPanelViewModel BuildVoicePanel(
        ulong guildId,
        IAudioService audioService,
        string? nowPlayingName)
    {
        var connectedChannelId = audioService.GetConnectedChannelId(guildId);
        var isConnected = audioService.IsConnected(guildId);
        PortalVoiceChannel? connectedChannel = isConnected && connectedChannelId.HasValue
            ? _guildDirectory.FindVoiceChannel(guildId, connectedChannelId.Value)
            : null;

        return new VoiceChannelPanelViewModel
        {
            GuildId = guildId,
            IsCompact = true,
            ShowNowPlaying = true,
            ShowProgress = false,
            IsConnected = isConnected,
            ConnectedChannelId = connectedChannelId,
            ConnectedChannelName = connectedChannel?.Name,
            ChannelMemberCount = connectedChannel?.MemberCount,
            AvailableChannels = _guildDirectory.GetVoiceChannels(guildId)
                .Select(c => new VoiceChannelInfo { Id = c.Id, Name = c.Name, MemberCount = c.MemberCount })
                .ToList(),
            NowPlaying = string.IsNullOrEmpty(nowPlayingName) ? null : new NowPlayingInfo { Name = nowPlayingName },
            Queue = [],
            ApiBase = $"/api/portal/soundboard/{guildId}"
        };
    }
}
