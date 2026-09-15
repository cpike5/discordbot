using Discord.WebSocket;
using DiscordBot.Bot.Extensions;
using DiscordBot.Bot.Services.Portal;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.DependencyInjection;

namespace DiscordBot.Bot.Pages.Portal;

/// <summary>
/// Base class for Portal pages that require guild membership authorization.
/// Provides common authorization logic while supporting the landing page UX pattern
/// where unauthenticated users see a landing page instead of being redirected.
/// </summary>
/// <remarks>
/// <see cref="CheckPortalAuthorizationAsync"/> delegates to <see cref="IPortalAccessService"/>
/// (<c>docs/plans/blazor-port-plan.md</c> §4.2, Phase 3) rather than running the three-state check
/// inline - the logic is shared with a future Blazor <c>PortalLayout</c>. The
/// <see cref="PortalAuthResult"/> enum and this class's public surface are unchanged so
/// <c>Portal/Soundboard</c>, <c>Portal/TTS</c>, <c>Portal/VOX</c> and the shared
/// <c>_PortalLanding</c>/<c>_PortalHeader</c> partials keep working without modification.
/// </remarks>
public abstract class PortalPageModelBase : PageModel
{
    private readonly DiscordSocketClient _discordClient;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="PortalPageModelBase"/> class.
    /// </summary>
    /// <remarks>
    /// This constructor's parameter list cannot change without also touching every derived
    /// Portal page model (Soundboard/TTS/VOX <c>IndexModel</c>), which is out of scope for this
    /// refactor - so rather than take <see cref="IPortalAccessService"/> as a fifth constructor
    /// parameter, it is resolved lazily from <see cref="PageModel.HttpContext"/>'s
    /// <c>RequestServices</c> the one time <see cref="CheckPortalAuthorizationAsync"/> needs it.
    /// This is the one service-locator exception in the codebase; see "Portal three-state gate"
    /// in <c>docs/architecture/patterns.md</c>.
    /// </remarks>
    protected PortalPageModelBase(
        IGuildService guildService,
        DiscordSocketClient discordClient,
        UserManager<ApplicationUser> userManager,
        ILogger logger)
    {
        // guildService and userManager are no longer used directly here - IPortalAccessService
        // (resolved in CheckPortalAuthorizationAsync) owns that lookup now - but both stay as
        // constructor parameters so every derived page model's existing
        // `: base(guildService, discordClient, userManager, logger)` call keeps compiling.
        _ = guildService;
        _ = userManager;
        _discordClient = discordClient;
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

        var portalAccessService = HttpContext.RequestServices.GetRequiredService<IPortalAccessService>();
        var returnPath = HttpContext.Request.Path.ToString();
        var access = await portalAccessService.ResolveAsync(guildId, User, returnPath, cancellationToken);

        if (access.Outcome == PortalAccessOutcome.GuildNotFound)
        {
            return (PortalAuthResult.GuildNotFound, null);
        }

        // Every other outcome carries a Context - set the properties every Portal page/partial
        // reads regardless of outcome (needed for both the landing page and the full portal).
        var portalContext = access.Context!;
        GuildId = guildId;
        GuildName = portalContext.GuildName;
        GuildIconUrl = portalContext.IconUrl;
        IsOnline = portalContext.IsBotOnline;
        LoginUrl = access.LoginUrl;

        // IPortalAccessService.PortalContext deliberately doesn't carry the Discord.Net
        // SocketGuild (see its XML doc) - PortalAuthContext still does, for the three Portal
        // Index pages that read context.SocketGuild directly for voice-channel listing, so it's
        // rebuilt here from the guild id the service already confirmed exists. GetGuild is an
        // in-memory gateway-cache read, not a network call, so re-resolving it is cheap.
        var socketGuild = _discordClient.GetGuild(guildId);
        if (socketGuild is null)
        {
            // The gateway cache lost the guild between IPortalAccessService's own existence check
            // and this re-lookup (e.g. the bot was removed from the guild in between) - every
            // outcome below builds a PortalAuthContext that assumes a non-null SocketGuild
            // (Soundboard/TTS/VOX Index pages dereference context!.SocketGuild unconditionally
            // once GetAuthResultAction lets them past ShowLandingPage), so this must short-circuit
            // to GuildNotFound the same way the original inline check always did, rather than let
            // ToAuthContext silently return a null Context for an "Authorized" result.
            return (PortalAuthResult.GuildNotFound, null);
        }

        switch (access.Outcome)
        {
            case PortalAccessOutcome.ShowLanding:
                IsAuthenticated = false;
                return (PortalAuthResult.ShowLandingPage, ToAuthContext(portalContext.Guild, socketGuild));

            case PortalAccessOutcome.NotGuildMember:
                IsAuthenticated = true;
                return (PortalAuthResult.NotGuildMember, ToAuthContext(portalContext.Guild, socketGuild));

            case PortalAccessOutcome.Authorized:
                IsAuthenticated = true;
                IsAuthorized = true;
                return (PortalAuthResult.Authorized, ToAuthContext(portalContext.Guild, socketGuild));

            default:
                return (PortalAuthResult.GuildNotFound, null);
        }
    }

    private static PortalAuthContext? ToAuthContext(GuildDto guild, SocketGuild? socketGuild) =>
        socketGuild is null ? null : new PortalAuthContext { Guild = guild, SocketGuild = socketGuild };

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
